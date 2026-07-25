using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TandisWebApp.Data;
using TandisWebApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1) Database Context - اتصال به همون SQL Server فعلی کیوسک
// ============================================================
builder.Services.AddDbContext<FullSportDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("FullSportDB"),
        sql => sql.CommandTimeout(60)
    ));

// ============================================================
// 2) JWT Authentication - احراز هویت با توکن برای موبایل اپ
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

    // JWT از کوکی "X-Access-Token" (اعضا) یا "X-Admin-Token" (مدیران) خوانده شود.
    // اولویت با کوکی ادمین است تا در صورت ورود مدیر، توکن عضو نادیده گرفته شود.
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
        // وقتی توکن منقضی شده یا معتبر نیست (401)، برای درخواست‌های MVC به صفحه لاگین ریدایرکت کن
        // برای درخواست‌های AJAX/API کد 401 برگردان تا سمت کلاینت هندل شود
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse(); // پیش‌فرض 401 را متوقف می‌کند

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
                // درخواست معمولی (مشاهده صفحه) -> به لاگین ریدایرکت
                var returnUrl = request.Path + request.QueryString;
                ctx.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            }
        }
    };
});

builder.Services.AddAuthorization();

// ============================================================
// 3) Business Services - لایه منطق برنامه
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
// سرویس‌های ادمین — IAdminUserProvider فعلاً hardcoded است و در آینده با DbAdminUserProvider جایگزین می‌شود
builder.Services.AddSingleton<IAdminUserProvider, HardcodedAdminUserProvider>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<AdminReportService>();

// HttpContextAccessor برای استفاده در Service‌ها
builder.Services.AddHttpContextAccessor();

// ============================================================
// 4) CORS - برای دسترسی موبایل اپ
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============================================================
// 5) MVC + API
// ============================================================
builder.Services.AddControllersWithViews();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB برای آپلود عکس
});

var app = builder.Build();

// ============================================================
// 5) Middleware Pipeline
// ============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// 6) Routes
// ============================================================

// مسیر ریشه: ریدایرکت به صفحه لاگین
app.MapGet("/", () => Results.Redirect("/Account/Login"));

// مسیر پیش‌فرض: اگر URL فقط controller باشه، action پیش‌فرض Index اجرا میشه
app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.Run();
