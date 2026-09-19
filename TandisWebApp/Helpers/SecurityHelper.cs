using System.Collections.Concurrent;

namespace TandisWebApp.Helpers
{
    public static class SecurityHelper
    {
        // ===== رمزنگاری قدیمی (ROT13 + پدینگ) — سازگار با داده‌های فعلی Sec_Users =====
        public static string Encrypt(string InputStr)
        {
            string OutPutStr = string.Empty;
            int i, j, k;
            char[] chrValue = InputStr.ToCharArray();
            for (i = 0; i <= chrValue.Length - 1; i++)
            {
                j = (int)chrValue[i];
                k = j;
                if (k >= 97 && k <= 109) k = k + 13;
                else if (k >= 110 && k <= 122) k = k - 13;
                else if (k >= 65 && k <= 77) k = k + 13;
                else if (k >= 78 && k <= 90) k = k - 13;
                OutPutStr = OutPutStr + "@!" + (char)k + "@%^" + (char)k;
            }
            return OutPutStr;
        }

        public static bool VerifyPassword(string plain, string stored)
            => string.Equals(Encrypt(plain ?? ""), stored ?? "", StringComparison.Ordinal);

        // ===== قفل پس از تلاش ناموفق =====
        private static readonly ConcurrentDictionary<string, (int Count, DateTime Last)> _fails = new();
        public const int MaxFails = 5;
        public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(10);

        private static string Key(string user, string ip) => (user + "|" + ip).ToLower();

        public static bool IsLocked(string user, string ip)
        {
            if (!_fails.TryGetValue(Key(user, ip), out var f)) return false;
            if (DateTime.Now - f.Last > LockDuration) { _fails.TryRemove(Key(user, ip), out _); return false; }
            return f.Count >= MaxFails;
        }

        public static void RecordFail(string user, string ip)
        {
            var k = Key(user, ip);
            _fails.AddOrUpdate(k, (1, DateTime.Now), (_, old) => (old.Count + 1, DateTime.Now));
        }

        public static void ClearFails(string user, string ip) => _fails.TryRemove(Key(user, ip), out _);

        public static int RemainingMinutes(string user, string ip)
        {
            if (!_fails.TryGetValue(Key(user, ip), out var f)) return 0;
            var left = LockDuration - (DateTime.Now - f.Last);
            return left > TimeSpan.Zero ? (int)Math.Ceiling(left.TotalMinutes) : 0;
        }
    }
}