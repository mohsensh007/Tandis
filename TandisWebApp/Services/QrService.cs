using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;

namespace TandisWebApp.Services
{
    public class QrService
    {
        private readonly FullSportDbContext _db;
        private readonly string _secretKey;

        public QrService(FullSportDbContext db, IConfiguration config)
        {
            _db = db;
            _secretKey = config["AppSettings:QrSecretKey"] ?? "DEFAULT_SECRET_KEY_CHANGE_THIS";
        }

        public async Task<string> GenerateQrPayloadAsync(short shiftID)
        {
            var system = await _db.Sec_Systems.FirstOrDefaultAsync();
            var systemCode = system?.SystemCode ?? "UNKNOWN";

            var now = DateTime.Now;
            var expireTime = now.AddMinutes(5);

            var payload = new QrPayload
            {
                SystemCode = systemCode,
                ShiftID = shiftID,
                IssuedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                ExpiresAt = expireTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Timestamp = now.Ticks
            };

            var json = JsonSerializer.Serialize(payload);
            payload.Signature = ComputeHMAC(json);

            return JsonSerializer.Serialize(payload);
        }

        public async Task<(bool isValid, string message)> ValidateQrPayloadAsync(string qrData)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<QrPayload>(qrData);
                if (payload == null)
                    return (false, "فرمت QR نامعتبر است");

                var jsonWithoutSig = JsonSerializer.Serialize(new QrPayload
                {
                    SystemCode = payload.SystemCode,
                    ShiftID = payload.ShiftID,
                    IssuedAt = payload.IssuedAt,
                    ExpiresAt = payload.ExpiresAt,
                    Timestamp = payload.Timestamp,
                    Signature = ""
                });

                var expectedSig = ComputeHMAC(jsonWithoutSig);
                if (payload.Signature != expectedSig)
                    return (false, "امضای QR نامعتبر است");

                if (DateTime.TryParse(payload.ExpiresAt, out var expireTime))
                {
                    if (DateTime.Now > expireTime)
                        return (false, "QR Code منقضی شده است");
                }

                var system = await _db.Sec_Systems.FirstOrDefaultAsync();
                if (system?.SystemCode != payload.SystemCode)
                    return (false, "SystemCode نامعتبر است");

                return (true, "QR Code معتبر است");
            }
            catch (Exception ex)
            {
                return (false, $"خطا در اعتبارسنجی: {ex.Message}");
            }
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