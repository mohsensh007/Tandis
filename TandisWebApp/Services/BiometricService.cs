using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TandisWebApp.Data;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>درخواست تکمیل ثبت بیومتریک (سمت مرورگر)</summary>
    public class BioRegisterRequest
    {
        public string? WebAuthnId { get; set; }
        public string? ClientDataJson { get; set; }
        public string? PublicKeySpki { get; set; }
        public int Alg { get; set; } = -7;
        public string? DeviceLabel { get; set; }
    }

    /// <summary>درخواست تأیید بیومتریک (ورود با اثر انگشت/چهره)</summary>
    public class BioAssertRequest
    {
        public string? WebAuthnId { get; set; }
        public string? ClientDataJson { get; set; }
        public string? AuthenticatorData { get; set; }
        public string? Signature { get; set; }
        public string? Purpose { get; set; }
    }

    /// <summary>
    /// احراز هویت بیومتریک بر پایهٔ WebAuthn (اثر انگشت در اندروید / Face ID در آیفون و ویندوز هلو).
    /// • ثبت (Enroll): کلید عمومی دستگاه در جدول Sec_BiometricCredential ذخیره می‌شود.
    /// • تأیید (Assert): امضای دستگاه با همان کلید عمومی روی سرور بررسی می‌شود.
    /// • نتیجهٔ تأیید یک «بلیت کوتاه‌عمر» است که به‌جای رمز عبور به اکشن‌های حساس داده می‌شود.
    /// </summary>
    public class BiometricService
    {
        private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan TicketTtl = TimeSpan.FromMinutes(3);

        private class ChallengeState
        {
            public string Challenge { get; set; } = "";
            public string Origin { get; set; } = "";
            public string RpId { get; set; } = "";
        }

        private class TicketState
        {
            public string UserType { get; set; } = "";
            public int UserID { get; set; }
            public string Purpose { get; set; } = "";
        }

        private readonly FullSportDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<BiometricService> _log;

        public BiometricService(FullSportDbContext db, IMemoryCache cache, ILogger<BiometricService> log)
        {
            _db = db;
            _cache = cache;
            _log = log;
        }

        // ============================================================
        //  کمکی‌ها
        // ============================================================
        private static string ChKey(string type, int id) => $"bio:ch:{type}:{id}";
        private static string TicketKey(string t) => $"bio:tk:{t}";

        public static string NewRandomToken() =>
            Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        public static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] Base64UrlDecode(string value)
        {
            var s = (value ?? "").Trim().Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

        public async Task<bool> HasCredentialAsync(string userType, int userID) =>
            await _db.Sec_BiometricCredentials.AsNoTracking()
                .AnyAsync(c => c.UserType == userType && c.UserID == userID);

        public async Task<int> CountAsync(string userType, int userID) =>
            await _db.Sec_BiometricCredentials.AsNoTracking()
                .CountAsync(c => c.UserType == userType && c.UserID == userID);

        // ============================================================
        //  ثبت (Enroll)
        // ============================================================
        public async Task<(bool ok, string message, object? data)> EnrollBeginAsync(
            string userType, int userID, string userName, string rpId, string origin)
        {
            var existing = await _db.Sec_BiometricCredentials.AsNoTracking()
                .Where(c => c.UserType == userType && c.UserID == userID)
                .Select(c => c.WebAuthnId)
                .ToListAsync();

            var challenge = NewRandomToken();
            _cache.Set(ChKey(userType, userID),
                new ChallengeState { Challenge = challenge, Origin = origin, RpId = rpId },
                ChallengeTtl);

            var data = new
            {
                challenge,
                rp = new { name = "ایمن تردد تندیس", id = rpId },
                user = new
                {
                    id = Base64UrlEncode(Encoding.UTF8.GetBytes($"{userType}:{userID}")),
                    name = string.IsNullOrWhiteSpace(userName) ? "user" : userName,
                    displayName = string.IsNullOrWhiteSpace(userName) ? "user" : userName
                },
                pubKeyCredParams = new object[]
                {
                    new { type = "public-key", alg = -7 },   // ES256 (اندروید / iOS / ویندوز)
                    new { type = "public-key", alg = -257 }  // RS256
                },
                timeout = 60000,
                attestation = "none",
                authenticatorSelection = new
                {
                    authenticatorAttachment = "platform",
                    userVerification = "required",
                    residentKey = "preferred",
                    requireResidentKey = false
                },
                excludeCredentials = existing
                    .Select(id => new { type = "public-key", id })
                    .ToArray()
            };

            return (true, "", data);
        }

        public async Task<(bool ok, string message)> EnrollCompleteAsync(
            string userType, int userID, string userName, string origin, BioRegisterRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.WebAuthnId) ||
                string.IsNullOrWhiteSpace(req.ClientDataJson) || string.IsNullOrWhiteSpace(req.PublicKeySpki))
                return (false, "اطلاعات ثبت بیومتریک ناقص است");

            if (!_cache.TryGetValue(ChKey(userType, userID), out ChallengeState? st) || st == null)
                return (false, "زمان ثبت به پایان رسیده است؛ دوباره تلاش کنید");

            var check = ValidateClientData(req.ClientDataJson, "webauthn.create", st, origin);
            if (!check.ok) return (false, check.message);

            if (req.Alg != -7 && req.Alg != -257)
                return (false, "الگوریتم دستگاه پشتیبانی نمی‌شود");

            // کلید عمومی باید قابل خواندن باشد (اعتبارسنجی قبل از ذخیره)
            try
            {
                var spki = Convert.FromBase64String(req.PublicKeySpki);
                if (req.Alg == -7)
                {
                    using var ec = ECDsa.Create();
                    ec.ImportSubjectPublicKeyInfo(spki, out _);
                }
                else
                {
                    using var rsa = RSA.Create();
                    rsa.ImportSubjectPublicKeyInfo(spki, out _);
                }
            }
            catch
            {
                return (false, "کلید عمومی دستگاه معتبر نیست");
            }

            var row = await _db.Sec_BiometricCredentials
                .FirstOrDefaultAsync(c => c.UserType == userType && c.UserID == userID
                                       && c.WebAuthnId == req.WebAuthnId);

            if (row == null)
            {
                row = new Sec_BiometricCredential
                {
                    UserType = userType,
                    UserID = userID,
                    WebAuthnId = req.WebAuthnId!,
                    CreatedAt = DateTime.Now
                };
                _db.Sec_BiometricCredentials.Add(row);
            }

            row.UserName = userName;
            row.PublicKeySpki = req.PublicKeySpki!;
            row.Alg = req.Alg;
            row.SignCount = 0;
            row.DeviceLabel = req.DeviceLabel;
            row.LastUsedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            _cache.Remove(ChKey(userType, userID));

            return (true, "اثر انگشت / چهره با موفقیت ثبت شد");
        }

        public async Task<(bool ok, string message)> DeleteAllAsync(string userType, int userID)
        {
            var rows = await _db.Sec_BiometricCredentials
                .Where(c => c.UserType == userType && c.UserID == userID)
                .ToListAsync();
            if (rows.Count == 0) return (false, "اثر انگشتی ثبت نشده است");

            _db.Sec_BiometricCredentials.RemoveRange(rows);
            await _db.SaveChangesAsync();
            return (true, "ثبت اثر انگشت حذف شد");
        }

        // ============================================================
        //  تأیید (Assert)
        // ============================================================
        public async Task<(bool ok, string message, object? data)> AssertBeginAsync(
            string userType, int userID, string rpId, string origin)
        {
            var creds = await _db.Sec_BiometricCredentials.AsNoTracking()
                .Where(c => c.UserType == userType && c.UserID == userID)
                .Select(c => c.WebAuthnId)
                .ToListAsync();

            if (creds.Count == 0)
                return (false, "برای این حساب اثر انگشت / چهره‌ای ثبت نشده است", null);

            var challenge = NewRandomToken();
            _cache.Set(ChKey(userType, userID),
                new ChallengeState { Challenge = challenge, Origin = origin, RpId = rpId },
                ChallengeTtl);

            var data = new
            {
                challenge,
                rpId,
                timeout = 60000,
                userVerification = "required",
                allowCredentials = creds
                    .Select(id => new { type = "public-key", id })
                    .ToArray()
            };

            return (true, "", data);
        }

        public async Task<(bool ok, string message, string? ticket)> AssertCompleteAsync(
            string userType, int userID, string origin, BioAssertRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.WebAuthnId) ||
                string.IsNullOrWhiteSpace(req.ClientDataJson) ||
                string.IsNullOrWhiteSpace(req.AuthenticatorData) ||
                string.IsNullOrWhiteSpace(req.Signature))
                return (false, "اطلاعات تأیید بیومتریک ناقص است", null);

            if (!_cache.TryGetValue(ChKey(userType, userID), out ChallengeState? st) || st == null)
                return (false, "زمان تأیید به پایان رسیده است؛ دوباره تلاش کنید", null);

            var check = ValidateClientData(req.ClientDataJson, "webauthn.get", st, origin);
            if (!check.ok) return (false, check.message, null);

            var cred = await _db.Sec_BiometricCredentials
                .FirstOrDefaultAsync(c => c.UserType == userType && c.UserID == userID
                                       && c.WebAuthnId == req.WebAuthnId);

            if (cred == null)
                return (false, "این اثر انگشت برای این حساب ثبت نشده است", null);

            byte[] authData, sig, clientHash;
            try
            {
                authData = Base64UrlDecode(req.AuthenticatorData!);
                sig = Base64UrlDecode(req.Signature!);
                clientHash = SHA256.HashData(Base64UrlDecode(req.ClientDataJson!));
            }
            catch
            {
                return (false, "داده‌های تأیید بیومتریک معتبر نیست", null);
            }

            if (authData.Length < 37)
                return (false, "داده‌های دستگاه معتبر نیست", null);

            // rpIdHash
            var expectedRpHash = SHA256.HashData(Encoding.UTF8.GetBytes(st.RpId));
            if (!authData.AsSpan(0, 32).SequenceEqual(expectedRpHash))
                return (false, "این دستگاه برای این دامنه ثبت نشده است", null);

            // UP (0x01) و UV (0x04)
            byte flags = authData[32];
            if ((flags & 0x01) == 0)
                return (false, "تأیید هویت دستگاه انجام نشد", null);
            if ((flags & 0x04) == 0)
                return (false, "لطفاً اثر انگشت یا چهره را تأیید کنید (تأیید کاربر لازم است)", null);

            var counter = BitConverter.ToUInt32(authData, 33);
            var signed = new byte[authData.Length + clientHash.Length];
            Buffer.BlockCopy(authData, 0, signed, 0, authData.Length);
            Buffer.BlockCopy(clientHash, 0, signed, authData.Length, clientHash.Length);

            bool signatureOk;
            try
            {
                var spki = Convert.FromBase64String(cred.PublicKeySpki);
                if (cred.Alg == -257)
                {
                    using var rsa = RSA.Create();
                    rsa.ImportSubjectPublicKeyInfo(spki, out _);
                    signatureOk = rsa.VerifyData(signed, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                else
                {
                    using var ec = ECDsa.Create();
                    ec.ImportSubjectPublicKeyInfo(spki, out _);
                    signatureOk = ec.VerifyData(signed, sig, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "بررسی امضای بیومتریک ناموفق بود (CredentialID={id})", cred.CredentialID);
                signatureOk = false;
            }

            if (!signatureOk)
                return (false, "تأیید اثر انگشت / چهره ناموفق بود", null);

            cred.SignCount = counter;
            cred.LastUsedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _cache.Remove(ChKey(userType, userID));

            var ticket = NewRandomToken();
            _cache.Set(TicketKey(ticket),
                new TicketState { UserType = userType, UserID = userID, Purpose = req.Purpose ?? "" },
                TicketTtl);

            return (true, "تأیید شد", ticket);
        }

        /// <summary>آیا این بلیت معتبر و متعلق به همین کاربر است؟ (یک‌بارمصرف)</summary>
        public bool ConsumeTicket(string? ticket, string userType, int userID)
        {
            if (string.IsNullOrWhiteSpace(ticket)) return false;

            if (!_cache.TryGetValue(TicketKey(ticket), out TicketState? st) || st == null)
                return false;

            if (st.UserType != userType || st.UserID != userID)
                return false;

            _cache.Remove(TicketKey(ticket));
            return true;
        }

        // ============================================================
        //  کمکی — بررسی clientDataJSON
        // ============================================================
        private static (bool ok, string message) ValidateClientData(
            string clientDataB64, string expectedType, ChallengeState st, string origin)
        {
            string json;
            try
            {
                json = Encoding.UTF8.GetString(Base64UrlDecode(clientDataB64));
            }
            catch
            {
                return (false, "داده‌های مرورگر معتبر نیست");
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var type) || type.GetString() != expectedType)
                    return (false, "نوع درخواست بیومتریک صحیح نیست");

                if (!root.TryGetProperty("challenge", out var ch) || ch.GetString() != st.Challenge)
                    return (false, "چالش امنیتی منقضی یا نامعتبر است؛ دوباره تلاش کنید");

                if (!root.TryGetProperty("origin", out var org))
                    return (false, "مبدأ درخواست مشخص نیست");

                var reqOrigin = (org.GetString() ?? "").TrimEnd('/');
                var expectedOrigin = (origin ?? "").TrimEnd('/');
                if (!string.Equals(reqOrigin, expectedOrigin, StringComparison.OrdinalIgnoreCase))
                    return (false, "مبدأ درخواست با سایت هم‌خوانی ندارد");
            }
            catch
            {
                return (false, "داده‌های مرورگر معتبر نیست");
            }

            return (true, "");
        }
    }
}
