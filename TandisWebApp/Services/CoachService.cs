using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    public class CoachService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;

        public CoachService(FullSportDbContext db, CommonHelperService helper)
        {
            _db = db;
            _helper = helper;
        }

        /// <summary>
        /// داشبورد مربی: آمار + کلاس‌های امروز
        /// </summary>
        public async Task<CoachDashboardDto> GetDashboardAsync(int coachMemberID)
        {
            var model = new CoachDashboardDto();

            // نام مربی
            model.CoachName = await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == coachMemberID
                select p.FullName
            ).FirstOrDefaultAsync() ?? "";

            // سانس‌های فعال من
            var mySanse = await _db.Gen_SportSanses
                .Where(s => s.CoachMemberID == coachMemberID && s.IsActive == true)
                .Select(s => s.SportSanseID)
                .ToListAsync();

            model.MyClassCount = mySanse.Count;

            // شاگردان فعال (بدون تکرار)
            model.ActiveStudents = mySanse.Count == 0 ? 0 : await _db.Acc_MemberSports
                .Where(a => mySanse.Contains(a.SportSanseID) && a.IsActive == true)
                .Select(a => a.MemberID)
                .Distinct()
                .CountAsync();

            // ========== کلاس‌های امروز (برنامه هفتگی) ==========
            var todayLatin = DateTime.Now.DayOfWeek.ToString();
            var todayStr = _helper.GetToday();
            var nowTime = DateTime.Now.TimeOfDay;

            var classes = await (
                from s in _db.Gen_SportSanses
                join d in _db.Set<Gen_SportSanseDetail>() on s.SportSanseID equals d.SportSanseID
                join w in _db.Set<Gen_DayOfWeek>() on d.DayID equals w.DayID
                where s.CoachMemberID == coachMemberID
                      && s.IsActive == true
                      && w.LatinName == todayLatin
                      && (d.IsActive == true || d.IsActive == null)
                orderby d.StartTime
                select new CoachClassDto
                {
                    SportSanseID = s.SportSanseID,
                    SportName = s.Gen_Sport_Category.SportName,
                    SanseName = s.SanseName,
                    DayName = w.DayName,
                    StartTime = d.StartTime,
                    EndTime = d.EndTime,
                    ClassCapacity = s.ClassCapacity,
                    RegCount = _db.Acc_MemberSports.Count(a => a.SportSanseID == s.SportSanseID && a.IsActive == true),
                    RegValidCount = _db.Acc_MemberSports.Count(a => a.SportSanseID == s.SportSanseID && a.IsActive == true && a.EndDate >= todayStr)
                }
            ).ToListAsync();

            // ========== حضور امروز هر کلاس ==========
            var sanseIds = classes.Select(c => c.SportSanseID).Distinct().ToList();
            var today = DateTime.Now.Date;
            var attendanceMap = new Dictionary<int, int>();

            if (sanseIds.Count > 0)
            {
                attendanceMap = await (
                    from t in _db.ACC_Traffics
                    join a in _db.Acc_MemberSports on t.SportMemberID equals a.SportMemberID
                    where sanseIds.Contains(a.SportSanseID)
                          && t.EntryDateTime != null
                          && t.EntryDateTime.Value.Date == today
                    group t by a.SportSanseID into g
                    select new { SanseID = g.Key, Cnt = g.Count() }
                ).ToDictionaryAsync(x => x.SanseID, x => x.Cnt);
            }

            // ========== وضعیت هر کلاس ==========
            foreach (var c in classes)
            {
                c.TodayAttendance = attendanceMap.TryGetValue(c.SportSanseID, out var cnt) ? cnt : 0;
                c.FreeNow = c.ClassCapacity.HasValue ? c.ClassCapacity.Value - c.RegValidCount : (int?)null;

                if (c.StartTime == null)
                {
                    c.Status = "unknown";
                    c.StatusDesc = "نامشخص";
                }
                else if (c.EndTime != null && nowTime >= c.StartTime.Value && nowTime <= c.EndTime.Value)
                {
                    c.Status = "ongoing";
                    c.StatusDesc = "در حال برگزاری";
                }
                else if (nowTime < c.StartTime.Value)
                {
                    c.Status = "upcoming";
                    c.StatusDesc = "امروز در پیش‌رو";
                }
                else
                {
                    c.Status = "done";
                    c.StatusDesc = "برگزار شده";
                }
            }

            model.TodayClasses = classes;
            model.TodayAttendance = classes.Sum(c => c.TodayAttendance);

            return model;
        }

        /// <summary>
        /// چک می‌کنه آیا عضو مربی هست یا نه
        /// </summary>
        public async Task<bool> IsCoachAsync(int memberID)
        {
            if (memberID == 0) return false;

            var roleID = await _db.Gen_Members
                .Where(m => m.MemberID == memberID)
                .Select(m => m.RoleID)
                .FirstOrDefaultAsync();

            return roleID == 2;
        }
    }
}