using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس «ورزشکاران»: وضعیت تردد + دفترچه تمرین + لاگ کمد + خلاصه امروز.
    /// ✅ کاملاً با LINQ (EF Core) — بدون هیچ SQL خام.
    /// ✅ هیچ جدولی نمی‌سازد؛ همه جدول‌ها باید از قبل در دیتابیس باشند:
    ///    - تردد:             dbo.ACC_Traffic        (فقط دستگاه می‌نویسد → اینجا فقط خواندنی)
    ///    - دفترچه تمرین:     dbo.ACC_AthleteSetLog  (Entity جدید بالا)
    ///    - نام حرکات:        dbo.Gen_PrgmItem
    ///    - لاگ باز کردن کمد: dbo.BoxLogOpenTb + dbo.Gen_Box
    /// </summary>
    public class AthleteService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;

        public AthleteService(FullSportDbContext db, CommonHelperService helper)
        {
            _db = db;
            _helper = helper;
        }

        // ============================================================
        //  تردد — فقط خواندن از دستگاه (dbo.ACC_Traffic)
        // ============================================================

        /// <summary>بازدیدِ امروز عضو از داده‌ی واقعی دستگاه تردد</summary>
        public async Task<AthleteVisitDto?> GetTodayVisitAsync(int memberID)
        {
            var today = _helper.GetToday();
            var list = await GetVisitsAsync(memberID, 30);
            return list.FirstOrDefault(v => NormDate(v.VisitDate) == today);
        }

        /// <summary>آخرین تردهای عضو (جدول تاریخچه‌ی صفحه)</summary>
        public async Task<List<AthleteVisitDto>> GetVisitsAsync(int memberID, int take = 10)
            => await QueryTrafficAsync(memberID, take);

        /// <summary>ترددهای واقعی عضو از جدول اصلی باشگاه (LINQ)</summary>
        private async Task<List<AthleteVisitDto>> QueryTrafficAsync(int memberID, int take)
        {
            var t = Math.Clamp(take, 1, 200);

            var rows = await _db.ACC_Traffics.AsNoTracking()
                .Where(x => x.MemberID == memberID)
                .OrderByDescending(x => x.TrafficID)
                .Take(t)
                .Select(x => new TrafficRow
                {
                    VisitID = x.TrafficID,
                    VisitDate = x.EntryDate,
                    EnterTime = x.EntryTime,
                    ExitTime = x.ExitTime,
                    SessionName = x.EntryDesc,
                    BoxID = x.BoxID ?? 0
                })
                .ToListAsync();

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

        /// <summary>آمار کارت وضعیت (هفته / ماه / روزهای پیاپی)</summary>
        public async Task<AthleteStatsDto> GetStatsAsync(int memberID, int setsDoneToday)
        {
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
        //  دفترچه تمرین — dbo.ACC_AthleteSetLog (LINQ)
        // ============================================================

        /// <summary>لاگ‌های یک روزِ عضو برای یک برنامه</summary>
        public async Task<List<AthleteSetLogDto>> GetLogsAsync(int memberID, int prgID, string? logDate)
        {
            var date = string.IsNullOrWhiteSpace(logDate) ? _helper.GetToday() : logDate!;

            var rows = await _db.ACC_AthleteSetLogs.AsNoTracking()
                .Where(s => s.MemberID == memberID && s.PrgID == prgID && s.LogDate == date)
                .OrderByDescending(s => s.LogID)
                .Select(s => new SetLogRow
                {
                    LogID = s.LogID,
                    ItemID = s.ItemID,
                    LogDate = s.LogDate,
                    DurationSec = s.DurationSec,
                    IsDone = s.IsDone,
                    Note = s.Note
                })
                .ToListAsync();

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

        /// <summary>ذخیره وضعیت یک حرکت (تیک + زمان). زمان هرگز کم نمی‌شود.</summary>
        public async Task SaveSetAsync(int memberID, int prgID, int itemID, bool done, int seconds, string? note, string? logDate)
        {
            var date = string.IsNullOrWhiteSpace(logDate) ? _helper.GetToday() : logDate!;
            var sec = Math.Max(0, seconds);

            var existing = await _db.ACC_AthleteSetLogs
                .Where(s => s.MemberID == memberID && s.PrgID == prgID && s.ItemID == itemID && s.LogDate == date)
                .OrderByDescending(s => s.LogID)
                .FirstOrDefaultAsync();

            if (existing == null)
            {
                _db.ACC_AthleteSetLogs.Add(new ACC_AthleteSetLog
                {
                    MemberID = memberID,
                    PrgID = prgID,
                    ItemID = itemID,
                    LogDate = date,
                    DurationSec = sec,
                    IsDone = done,
                    Note = string.IsNullOrWhiteSpace(note) ? null : note,
                    CreationDateTime = DateTime.Now
                });
            }
            else
            {
                existing.IsDone = done;
                existing.DurationSec = Math.Max(existing.DurationSec, sec);   // ✅ زمان کم نمی‌شود
                if (!string.IsNullOrWhiteSpace(note)) existing.Note = note;
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>تعداد حرکات انجام‌شده‌ی امروز عضو (برای آمار صفحه)</summary>
        public async Task<int> CountDoneTodayAsync(int memberID)
        {
            var today = _helper.GetToday();
            return await _db.ACC_AthleteSetLogs
                .CountAsync(s => s.MemberID == memberID && s.LogDate == today && s.IsDone);
        }

        // ============================================================
        //  ✅ خلاصه تمرین امروز — نمودار دایره‌ای (LINQ group+join)
        // ============================================================
        public async Task<List<AthleteSummaryItemDto>> GetTodaySummaryAsync(int memberID)
        {
            var today = _helper.GetToday();

            var q = from s in _db.ACC_AthleteSetLogs.AsNoTracking()
                    join g in _db.Gen_PrgmItems.AsNoTracking() on s.ItemID equals g.ItemID
                    where s.MemberID == memberID && s.LogDate == today && s.IsDone && s.DurationSec > 0
                    group s by g.ItemDesc into grp
                    select new AthleteSummaryItemDto
                    {
                        Name = grp.Key ?? "-",
                        Minutes = Math.Round(grp.Sum(x => x.DurationSec) / 60.0, 1)
                    };

            var list = await q.ToListAsync();
            return list.OrderByDescending(x => x.Minutes).ToList();
        }
        // ============================================================
        //  لاگ باز کردن کمد — dbo.BoxLogOpenTb (LINQ)

        public async Task LogLockerOpenAsync(int memberID, short lockerRoomID, short boxNo, bool success, string? message)
        {
            try
            {
                // ✅ پیدا کردن BoxID از روی رختکن + شماره کمد (LINQ)
                var boxId = await _db.Gen_Boxes.AsNoTracking()
                    .Where(b => b.LockerRoomID == lockerRoomID && b.BoxNo == boxNo)
                    .Select(b => b.BoxID)
                    .FirstOrDefaultAsync();

                var now = DateTime.Now;

                _db.BoxLogOpenTbs.Add(new BoxLogOpenTb
                {
                    MemberID = memberID,
                    LockerRoomID = lockerRoomID,
                    BoxNo = boxNo,
                    BoxID = boxId == 0 ? null : (short?)boxId,
                    IsSuccess = success,
                    Message = string.IsNullOrWhiteSpace(message) ? null : message,
                    OpenDate = now,
                    CreationDateTime = now
                    // TerminalID دست‌نخورده NULL می‌مونه
                });

                await _db.SaveChangesAsync();
            }
            catch
            {
                // لاگ کمد فقط Audit هست — نباید جریان اصلی رو بشکنه
            }
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

        internal static string ShamsiOf(DateTime d)
        {
            var pc = new PersianCalendar();
            return $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";
        }

        private static DateTime FromShamsiOrToday(string shamsi)
        {
            try
            {
                var p = shamsi.Split('/');
                if (p.Length == 3)
                {
                    var pc = new PersianCalendar();
                    return pc.ToDateTime(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), 0, 0, 0, 0);
                }
            }
            catch { }
            return DateTime.Now.Date;
        }

        private static string NormDate(string? d)
        {
            var p = (d ?? "").Replace('-', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length != 3) return "";
            if (!int.TryParse(p[0], out var y) || !int.TryParse(p[1], out var m) || !int.TryParse(p[2], out var day)) return "";
            return $"{y:0000}/{m:00}/{day:00}";
        }

        // ===== ردیف‌های واسط (فقط برای map بعد از ToList) =====
        public class TrafficRow
        {
            public long VisitID { get; set; }
            public string VisitDate { get; set; } = "";
            public string EnterTime { get; set; } = "";
            public string? ExitTime { get; set; }
            public string? SessionName { get; set; }
            public short BoxID { get; set; }
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
    }
}