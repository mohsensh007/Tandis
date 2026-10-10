using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس «ورزشکاران»: ثبت ورود/خروج باشگاه + دفترچه تمرین (لاگ حرکات).
    /// جدول‌های مخصوص وب‌اپ را در اولین استفاده می‌سازد (بدون دست‌زدن به جدول‌های اصلی باشگاه).
    /// </summary>
    public class AthleteService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;

        private static int _ensured; // 0 = هنوز ساخته نشده

        public AthleteService(FullSportDbContext db, CommonHelperService helper)
        {
            _db = db;
            _helper = helper;
        }

        // ============================================================
        //  ساخت جدول‌ها (Idempotent)
        // ============================================================
        private const string EnsureVisitSql = @"
IF OBJECT_ID(N'dbo.Web_AthleteVisit', N'U') IS NULL
CREATE TABLE dbo.Web_AthleteVisit (
    VisitID          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MemberID         INT               NOT NULL,
    VisitDate        NVARCHAR(10)      NOT NULL,
    EnterTime        NVARCHAR(8)       NOT NULL,
    ExitTime         NVARCHAR(8)       NULL,
    SessionName      NVARCHAR(100)     NULL,
    CreationDateTime DATETIME          NOT NULL DEFAULT GETDATE()
);
";

        private const string EnsureSetLogSql = @"
IF OBJECT_ID(N'dbo.Web_AthleteSetLog', N'U') IS NULL
CREATE TABLE dbo.Web_AthleteSetLog (
    LogID            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MemberID         INT               NOT NULL,
    PrgID            INT               NOT NULL,
    ItemID           INT               NOT NULL,
    LogDate          NVARCHAR(10)      NOT NULL,
    DurationSec      INT               NOT NULL DEFAULT 0,
    IsDone           BIT               NOT NULL DEFAULT 0,
    Note             NVARCHAR(200)     NULL,
    CreationDateTime DATETIME          NOT NULL DEFAULT GETDATE()
);
";

        private const string EnsureLockerLogSql = @"
IF OBJECT_ID(N'dbo.Web_LockerOpenLog', N'U') IS NULL
CREATE TABLE dbo.Web_LockerOpenLog (
    LogID            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    MemberID         INT               NOT NULL,
    LockerRoomID     SMALLINT          NOT NULL,
    BoxNo            SMALLINT          NOT NULL,
    IsSuccess        BIT               NOT NULL DEFAULT 0,
    Message          NVARCHAR(200)     NULL,
    CreationDateTime DATETIME          NOT NULL DEFAULT GETDATE()
);
";

        private static readonly SemaphoreSlim _ensureLock = new(1, 1);

        private async Task EnsureTablesAsync()
        {
            if (Volatile.Read(ref _ensured) == 1) return;
            await _ensureLock.WaitAsync();
            try
            {
                if (Volatile.Read(ref _ensured) == 1) return;
                await _db.Database.ExecuteSqlRawAsync(EnsureVisitSql);
                await _db.Database.ExecuteSqlRawAsync(EnsureSetLogSql);
                await _db.Database.ExecuteSqlRawAsync(EnsureLockerLogSql);
                Volatile.Write(ref _ensured, 1);
            }
            finally
            {
                _ensureLock.Release();
            }
        }

        // ============================================================
        //  helper های ADO (چون SqlQuery پارامتردار در EF Core 10 نیست)
        // ============================================================
        private async Task<List<T>> QueryAsync<T>(string sql, params (string Name, object Value)[] pars) where T : new()
        {
            var conn = _db.Database.GetDbConnection();
            await _db.Database.OpenConnectionAsync();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                foreach (var (name, value) in pars)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = name;
                    p.Value = value ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }

                var list = new List<T>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var obj = new T();
                    foreach (var prop in typeof(T).GetProperties())
                    {
                        var ord = reader.GetOrdinal(prop.Name);
                        if (ord < 0) continue;
                        if (!reader.IsDBNull(ord))
                            prop.SetValue(obj, Convert.ChangeType(reader.GetValue(ord), prop.PropertyType));
                    }
                    list.Add(obj);
                }
                return list;
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }

        private async Task<int> ExecAsync(string sql, params (string Name, object Value)[] pars)
        {
            return await _db.Database.ExecuteSqlRawAsync(sql,
                pars.Select(p => new SqlParameter(p.Name, p.Value ?? DBNull.Value)).ToArray());
        }

        // ============================================================
        //  ورود / خروج
        // ============================================================

        /// <summary>✅ بازدیدِ امروز — از داده‌ی واقعی دستگاه تردد باشگاه (dbo.ACC_Traffic).</summary>
        /// <remarks>ثبت ورود/خروجِ داخل اپ حذف شد؛ عضو فقط وضعیت و ساعت را می‌بیند.</remarks>
        public async Task<AthleteVisitDto?> GetTodayVisitAsync(int memberID)
        {
            var today = _helper.GetToday();
            var list = await GetVisitsAsync(memberID, 30);
            return list.FirstOrDefault(v => NormDate(v.VisitDate) == today);
        }

        /// <summary>آخرین تردهای عضو (جدول تاریخچه‌ی صفحه)</summary>
        public async Task<List<AthleteVisitDto>> GetVisitsAsync(int memberID, int take = 10)
        {
            return await QueryTrafficAsync(memberID, take);
        }

        /// <summary>ترددهای واقعی عضو از جدول اصلی باشگاه (توسط دستگاه تردد ثبت می‌شود)</summary>
        private async Task<List<AthleteVisitDto>> QueryTrafficAsync(int memberID, int take)
        {
            var rows = await QueryAsync<TrafficRow>(
                $@"SELECT TOP({Math.Clamp(take, 1, 200)}) TrafficID AS VisitID, EntryDate AS VisitDate,
                          EntryTime AS EnterTime, ExitTime, EntryDesc AS SessionName, BoxID
                   FROM dbo.ACC_Traffic
                   WHERE MemberID = @m
                   ORDER BY TrafficID DESC", ("@m", memberID));

            var now = DateTime.Now.TimeOfDay;
            return rows.Select(r =>
            {
                var t1 = ParseTime(r.EnterTime);
                int? mins = null;
                if (t1 != null)
                {
                    var end = ParseTime(r.ExitTime) ?? now;
                    var diff = end - t1.Value;
                    if (diff >= TimeSpan.Zero) mins = (int)Math.Round(diff.TotalMinutes);
                }
                return new AthleteVisitDto
                {
                    VisitID = r.VisitID,
                    VisitDate = r.VisitDate,
                    EnterTime = r.EnterTime,
                    ExitTime = r.ExitTime,
                    SessionName = r.SessionName,
                    BoxID = r.BoxID == 0 ? null : r.BoxID,
                    IsOpen = string.IsNullOrWhiteSpace(r.ExitTime),
                    DurationMinutes = mins
                };
            }).ToList();
        }

        /// <summary>ثبت ورود به باشگاه. اگر ورودِ بازِ امروز وجود داشته باشد همان برمی‌گردد.</summary>
        public async Task<AthleteVisitDto> CheckInAsync(int memberID, string? enterTime, string? sessionName)
        {
            await EnsureTablesAsync();
            var today = _helper.GetToday();

            var open = await GetTodayVisitAsync(memberID);
            if (open != null && open.IsOpen) return open;

            var time = NormalizeTime(enterTime) ?? _helper.GetThisTime();
            object sess = string.IsNullOrWhiteSpace(sessionName) ? DBNull.Value : sessionName.Trim();

            await ExecAsync(
                @"INSERT INTO dbo.Web_AthleteVisit (MemberID, VisitDate, EnterTime, SessionName)
                  VALUES (@m, @d, @t, @s)",
                ("@m", memberID), ("@d", today), ("@t", time), ("@s", sess));

            return new AthleteVisitDto
            {
                VisitDate = today,
                EnterTime = time,
                SessionName = sessionName?.Trim(),
                IsOpen = true
            };
        }

        /// <summary>ثبت خروج (ورودِ بازِ امروز)</summary>
        public async Task<AthleteVisitDto?> CheckOutAsync(int memberID)
        {
            await EnsureTablesAsync();
            var today = _helper.GetToday();
            var now = _helper.GetThisTime();

            var affected = await ExecAsync(
                @"UPDATE dbo.Web_AthleteVisit SET ExitTime = @x
                  WHERE VisitID = (SELECT TOP(1) VisitID FROM dbo.Web_AthleteVisit
                                   WHERE MemberID = @m AND VisitDate = @d AND ExitTime IS NULL
                                   ORDER BY VisitID DESC)",
                ("@x", now), ("@m", memberID), ("@d", today));

            if (affected == 0) return null;
            return await GetTodayVisitAsync(memberID);
        }

        /// <summary>آمار کارت وضعیت</summary>
        public async Task<AthleteStatsDto> GetStatsAsync(int memberID, int setsDoneToday)
        {
            await EnsureTablesAsync();
            var list = await QueryTrafficAsync(memberID, 120);

            var st = new AthleteStatsDto { SetsDoneToday = setsDoneToday };
            var todayDate = DateTime.Now.Date;

            foreach (var v in list)
            {
                var d = FromShamsiOrToday(v.VisitDate);
                if ((todayDate - d).TotalDays < 7) st.VisitsThisWeek++;
                if (d.Year == todayDate.Year && d.Month == todayDate.Month)
                {
                    st.VisitsThisMonth++;
                    if (v.DurationMinutes > 0) st.TotalMinutesThisMonth += v.DurationMinutes.Value;
                }
            }

            // روزهای پیاپی حضور
            var dates = list.Select(v => v.VisitDate).Distinct().ToHashSet();
            var cursor = todayDate;
            if (!dates.Contains(ShamsiOf(cursor))) cursor = cursor.AddDays(-1);
            while (dates.Contains(ShamsiOf(cursor)))
            {
                st.StreakDays++;
                cursor = cursor.AddDays(-1);
            }
            return st;
        }

        // ============================================================
        //  دفترچه تمرین
        // ============================================================

        /// <summary>لاگ‌های یک روزِ عضو برای یک برنامه</summary>
        public async Task<List<AthleteSetLogDto>> GetLogsAsync(int memberID, int prgID, string? logDate)
        {
            await EnsureTablesAsync();
            var date = string.IsNullOrWhiteSpace(logDate) ? _helper.GetToday() : logDate!;
            var rows = await QueryAsync<SetLogRow>(
                @"SELECT LogID, ItemID, LogDate, DurationSec, IsDone, Note
                  FROM dbo.Web_AthleteSetLog
                  WHERE MemberID = @m AND PrgID = @p AND LogDate = @d",
                ("@m", memberID), ("@p", prgID), ("@d", date));

            return rows
                .GroupBy(x => x.ItemID)
                .Select(g => g.OrderByDescending(x => x.LogID).First())
                .Select(x => new AthleteSetLogDto
                {
                    ItemID = x.ItemID,
                    LogDate = x.LogDate,
                    IsDone = x.IsDone,
                    DurationSec = x.DurationSec,
                    Note = x.Note
                })
                .ToList();
        }

        /// <summary>ذخیره وضعیت یک حرکت (انجام‌شده + زمان سپری‌شده). زمان هرگز کم نمی‌شود.</summary>
        public async Task SaveSetAsync(int memberID, int prgID, int itemID, bool done, int seconds, string? note, string? logDate)
        {
            await EnsureTablesAsync();
            var date = string.IsNullOrWhiteSpace(logDate) ? _helper.GetToday() : logDate!;

            var existing = await QueryAsync<ScalarId>(
                @"SELECT TOP(1) LogID AS V
                  FROM dbo.Web_AthleteSetLog
                  WHERE MemberID = @m AND PrgID = @p AND ItemID = @i AND LogDate = @d
                  ORDER BY LogID DESC",
                ("@m", memberID), ("@p", prgID), ("@i", itemID), ("@d", date));

            object noteVal = string.IsNullOrWhiteSpace(note) ? DBNull.Value : note;

            if (existing.Count == 0)
            {
                await ExecAsync(
                    @"INSERT INTO dbo.Web_AthleteSetLog (MemberID, PrgID, ItemID, LogDate, DurationSec, IsDone, Note)
                      VALUES (@m, @p, @i, @d, @s, @done, @n)",
                    ("@m", memberID), ("@p", prgID), ("@i", itemID), ("@d", date),
                    ("@s", Math.Max(0, seconds)), ("@done", done), ("@n", noteVal));
            }
            else
            {
                await ExecAsync(
                    @"UPDATE dbo.Web_AthleteSetLog
                      SET IsDone = @done,
                          DurationSec = CASE WHEN @s > DurationSec THEN @s ELSE DurationSec END,
                          Note = CASE WHEN @n IS NULL THEN Note ELSE @n END
                      WHERE LogID = @id",
                    ("@done", done), ("@s", Math.Max(0, seconds)), ("@n", noteVal), ("@id", existing[0].V));
            }
        }

        /// <summary>تعداد حرکات انجام‌شده امروزِ عضو (برای آمار صفحه)</summary>
        public async Task<int> CountDoneTodayAsync(int memberID)
        {
            await EnsureTablesAsync();
            var today = _helper.GetToday();
            var rows = await QueryAsync<ScalarId>(
                @"SELECT COUNT(*) AS V FROM dbo.Web_AthleteSetLog
                  WHERE MemberID = @m AND LogDate = @d AND IsDone = 1",
                ("@m", memberID), ("@d", today));
            return rows.Count == 0 ? 0 : rows[0].V;
        }

        /// <summary>ثبت لاگ باز کردن کمد (برای تاریخچه صفحه)</summary>
        public async Task LogLockerOpenAsync(int memberID, short lockerRoomID, short boxNo, bool success, string? message)
        {
            await EnsureTablesAsync();
            object msg = string.IsNullOrWhiteSpace(message) ? DBNull.Value : message;
            await ExecAsync(
                @"INSERT INTO dbo.Web_LockerOpenLog (MemberID, LockerRoomID, BoxNo, IsSuccess, Message)
                  VALUES (@m, @r, @b, @ok, @msg)",
                ("@m", memberID), ("@r", lockerRoomID), ("@b", boxNo), ("@ok", success), ("@msg", msg));
        }

        // ============================================================
        //  کمکی‌ها
        // ============================================================
        private static TimeSpan? ParseTime(string? hhmm)
        {
            if (string.IsNullOrWhiteSpace(hhmm)) return null;
            var parts = hhmm.Split(':');
            if (parts.Length < 2) return null;
            if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m)) return null;
            return new TimeSpan(Math.Clamp(h, 0, 23), Math.Clamp(m, 0, 59), 0);
        }

        private static string? NormalizeTime(string? input)
        {
            var t = ParseTime(input);
            return t == null ? null : t.Value.ToString(@"hh\:mm");
        }

        internal static string ShamsiOf(DateTime d)
        {
            var pc = new System.Globalization.PersianCalendar();
            return $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";
        }

        private static DateTime FromShamsiOrToday(string shamsi)
        {
            try
            {
                var p = shamsi.Split('/');
                if (p.Length == 3)
                {
                    var pc = new System.Globalization.PersianCalendar();
                    return pc.ToDateTime(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), 0, 0, 0, 0);
                }
            }
            catch { }
            return DateTime.Now.Date;
        }

        // ===== ردیف‌های ساده برای QueryAsync =====
        public class VisitRow
        {
            public int VisitID { get; set; }
            public string VisitDate { get; set; } = "";
            public string EnterTime { get; set; } = "";
            public string? ExitTime { get; set; }
            public string? SessionName { get; set; }
        }

        /// <summary>ردیف دستگاه تردد (dbo.ACC_Traffic)</summary>
        public class TrafficRow
        {
            public long VisitID { get; set; }
            public string VisitDate { get; set; } = "";
            public string EnterTime { get; set; } = "";
            public string? ExitTime { get; set; }
            public string? SessionName { get; set; }
            public short BoxID { get; set; }
        }

        /// <summary>نرمال‌سازی تاریخ شمسی (1405/7/9 → 1405/07/09) برای مقایسه</summary>
        private static string NormDate(string? d)
        {
            var p = (d ?? "").Replace('-', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length != 3) return "";
            if (!int.TryParse(p[0], out var y) || !int.TryParse(p[1], out var m) || !int.TryParse(p[2], out var day)) return "";
            return $"{y:0000}/{m:00}/{day:00}";
        }

        public class SetLogRow
        {
            public int LogID { get; set; }
            public int ItemID { get; set; }
            public string LogDate { get; set; } = "";
            public int DurationSec { get; set; }
            public bool IsDone { get; set; }
            public string? Note { get; set; }
        }

        public class ScalarId
        {
            public int V { get; set; }
        }
    }
}
