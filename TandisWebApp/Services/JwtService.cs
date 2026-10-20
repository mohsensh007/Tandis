using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TandisWebApp.Services
{
    /// <summary>
    /// تولید و مدیریت توکن JWT برای احراز هویت کاربران
    /// </summary>
    public class JwtService
    {
        // فقط برای اجرای Development؛ secret واقعی باید از environment بیاید.
        public const string DevelopmentSecretKey = "TandisWebApp-development-only-key-change-me-2026-unsafe";

        private readonly IConfiguration _config;
        private readonly IHostEnvironment _environment;

        public JwtService(IConfiguration config, IHostEnvironment environment)
        {
            _config = config;
            _environment = environment;
        }

        /// <summary>ساخت توکن JWT برای یک عضو</summary>
        public string GenerateToken(int memberID, string fullName, string? mobile, short? shiftID,int roleID =1)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = ResolveSecretKey();
            var issuer = jwtSettings["Issuer"] ?? "TandisWebApp";
            var audience = jwtSettings["Audience"] ?? "TandisWebAppUsers";
            var expiryMin = int.Parse(jwtSettings["ExpiryMinutes"] ?? "1440");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, memberID.ToString()),
                new Claim(ClaimTypes.Name, fullName),
                new Claim("MemberID", memberID.ToString()),
                new Claim("ShiftID", (shiftID ?? 1).ToString()),
                new Claim("FullName", fullName),
                new Claim("RoleID", roleID.ToString())
            };
            if (!string.IsNullOrEmpty(mobile))
                claims.Add(new Claim(ClaimTypes.MobilePhone, mobile));

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMin),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>ساخت توکن JWT برای یک مدیر (مجزا از توکن اعضا).</summary>
        /// <param name="username">نام کاربری مدیر</param>
        /// <param name="displayName">نام نمایشی مدیر</param>
        /// <param name="shiftID">شماره شیفت مجاز (۱=آقایان، ۲=بانوان)</param>
        public string GenerateAdminToken(short userID, string username, string displayName, short shiftID)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = ResolveSecretKey();
            var issuer = jwtSettings["Issuer"] ?? "TandisWebApp";
            var audience = jwtSettings["Audience"] ?? "TandisWebAppUsers";
            var expiryMin = int.Parse(jwtSettings["ExpiryMinutes"] ?? "1440");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, username),
        new Claim(ClaimTypes.Name, displayName),
        new Claim("IsAdmin", "true"),
        new Claim("AdminUsername", username),
        new Claim("AdminShiftID", shiftID.ToString()),
        new Claim("FullName", displayName),
        new Claim("UserID", userID.ToString())   // ✅ جدید
    };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMin),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string ResolveSecretKey()
        {
            var configuredSecretKey = _config["JwtSettings:SecretKey"];
            if (!string.IsNullOrWhiteSpace(configuredSecretKey))
                return configuredSecretKey;

            if (_environment.IsDevelopment())
                return DevelopmentSecretKey;

            throw new InvalidOperationException(
                "JwtSettings:SecretKey must be provided through environment configuration in production");
        }
    }
}
