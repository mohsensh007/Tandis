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

    // JWT از کوکی "X-Access-Token" هم خوانده شود (برای صفحات Razor)
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Cookies["X-Access-Token"];
            if (!string.IsNullOrEmpty(token))
                ctx.Token = token;
            return Task.CompletedTask;
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
