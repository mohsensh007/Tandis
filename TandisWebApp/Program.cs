using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using System.Text;
using TandisWebApp.Data;
using TandisWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 0) فشرده‌سازی پاسخ (Brotli + Gzip)
// ============================================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/html", "text/css", "text/javascript", "application/javascript",
        "application/json", "text/xml", "application/xml",
        "image/svg+xml", "font/woff2"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.SmallestSize);
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
    options.Level = CompressionLevel.SmallestSize);

// ============================================================
// 1) Database Context
// ============================================================
builder.Services.AddDbContext<FullSportDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("FullSportDB"),
        sql => sql.CommandTimeout(60)
    ));

// ============================================================
// 2) JWT Authentication
// ============================================================
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey missing");
var issuer = jwtSettings["Issuer"] ?? "TandisWebApp";
var audience = jwtSettings["Audience"] ?? "TandisWebAppUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var adminToken = ctx.Request.Cookies["X-Admin-Token"];
            if (!string.IsNullOrEmpty(adminToken))
            {
                ctx.Token = adminToken;
                return Task.CompletedTask;
            }
            var memberToken = ctx.Request.Cookies["X-Access-Token"];
            if (!string.IsNullOrEmpty(memberToken))
                ctx.Token = memberToken;
            return Task.CompletedTask;
        },
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            var request = ctx.Request;
            var isAjax = request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                         request.Headers["Accept"].ToString().Contains("application/json") ||
                         request.Path.StartsWithSegments("/api");

            if (isAjax)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsJsonAsync(new { success = false, message = "نشست منقضی شده است. لطفاً مجدداً وارد شوید.", redirectTo = "/Account/Login" });
            }
            else
            {
                var returnUrl = request.Path + request.QueryString;
                ctx.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            }
        }
    };
});

builder.Services.AddAuthorization();

// ============================================================
// 3) Business Services
// ============================================================
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<MemberAuthService>();
builder.Services.AddScoped<RegisterService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<ShopService>();
builder.Services.AddScoped<ServicePurchaseService>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<CommonHelperService>();
builder.Services.AddScoped<TandisWebApp.Services.QrService>();
builder.Services.AddScoped<CoachProgramService>();

builder.Services.AddScoped<IAdminUserProvider, DbAdminUserProvider>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<AdminReportService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<CoachService>();

builder.Services.AddHttpContextAccessor();

// ============================================================
// 4) CORS
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:5500", "http://localhost:5104", "http://localhost:5200", "http://localhost:5300", "http://localhost:5400", "http://localhost:5501")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ============================================================
// 5) MVC + API + کش + Rate Limiting
// ============================================================
builder.Services.AddControllersWithViews();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
});

builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    options.OnRejected = (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = "تعداد درخواست‌ها زیاد است. لطفاً ۱ دقیقه صبر کنید."
        });
        return ValueTask.CompletedTask;
    };
});

var app = builder.Build();

// ============================================================
// 6) Middleware Pipeline
// ============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ✅ فشرده‌سازی
app.UseResponseCompression();

// ✅ فایل‌های استاتیک با کش ۱ ساله
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000,immutable");
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    }
});

app.UseRouting();
app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ✅ Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// ============================================================
// 7) Routes
// ============================================================
app.MapGet("/", () => Results.Redirect("/Account/Login"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.Run();