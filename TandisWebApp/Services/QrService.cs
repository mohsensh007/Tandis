using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TandisWebApp.Data;

namespace TandisWebApp.Services
{
    public class QrService
    {
        private readonly FullSportDbContext _db;
        private readonly string _secretKey;
        private readonly IMemoryCache _cache;
        private const string CACHE_PREFIX = "QR_TOKEN_";

        public QrService(FullSportDbContext db, IConfiguration config, IMemoryCache cache)
        {
            _db = db;
            _secretKey = config["AppSettings:QrSecretKey"] ?? "DEFAULT_SECRET_KEY_CHANGE_THIS";
            _cache = cache;
        }

        /// <summary>
        /// تولید توکن QR — محتوای QR فقط همین توکن کوتاهه (QR کم‌تراکم و خوانا)
        /// </summary>
        public async Task<string> GenerateQrTokenAsync(short shiftID)
        {
            var system = await _db.Sec_Systems.FirstOrDefaultAsync();
            var systemCode = system?.SystemCode ?? "UNKNOWN";

            var now = DateTime.Now;
            var payload = new QrPayload
            {
                SystemCode = systemCode,
                ShiftID = shiftID,
                IssuedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                ExpiresAt = now.AddMinutes(5).ToString("yyyy-MM-dd HH:mm:ss"),
                Timestamp = now.Ticks,
                Signature = ""
            };

            var json = JsonSerializer.Serialize(payload);
            payload.Signature = ComputeHMAC(json);

            // توکن ۱۶ کاراکتری → QR نسخه ~۲ (۲۵×۲۵) به جای نسخه ۱۰+ (۵۷×۵۷)
            var token = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();

            // payload کامل در حافظه سرور، با انقضای ۵ دقیقه
            _cache.Set(CACHE_PREFIX + token, payload, TimeSpan.FromMinutes(5));

            return token;
        }

        /// <summary>
        /// اعتبارسنجی توکن اسکن‌شده
        /// </summary>
        public Task<(bool isValid, string message)> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Task.FromResult((false, "فرمت QR نامعتبر است"));

            var key = CACHE_PREFIX + token.Trim().ToUpper();

            // ✅ نسخه ژنریک:
            if (!_cache.TryGetValue<QrPayload>(key, out var payload) || payload == null)
                return Task.FromResult((false, "QR Code منقضی شده است. لطفاً QR جدید روی صفحه باشگاه را اسکن کنید."));

            var jsonWithoutSig = JsonSerializer.Serialize(new QrPayload
            {
                SystemCode = payload.SystemCode,
                ShiftID = payload.ShiftID,
                IssuedAt = payload.IssuedAt,
                ExpiresAt = payload.ExpiresAt,
                Timestamp = payload.Timestamp,
                Signature = ""
            });

            if (payload.Signature != ComputeHMAC(jsonWithoutSig))
                return Task.FromResult((false, "امضای QR نامعتبر است"));

            if (DateTime.TryParse(payload.ExpiresAt, out var expireTime) && DateTime.Now > expireTime)
                return Task.FromResult((false, "QR Code منقضی شده است."));

            return Task.FromResult((true, "QR Code معتبر است"));
        }

        private string ComputeHMAC(string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hash);
        }
    }

    public class QrPayload
    {
        public string SystemCode { get; set; } = "";
        public short ShiftID { get; set; }
        public string IssuedAt { get; set; } = "";
        public string ExpiresAt { get; set; } = "";
        public long Timestamp { get; set; }
        public string Signature { get; set; } = "";
    }
}