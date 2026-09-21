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

        // ========== داشبورد مربی ==========
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

            var todayStr = _helper.GetToday();
            var todayLatin = DateTime.Now.DayOfWeek.ToString();
            var nowTime = DateTime.Now.TimeOfDay;

            // ✅ فیکس CS1503: فیلتر null + .Value
            model.ActiveStudents = mySanse.Count == 0 ? 0 : await _db.Acc_MemberSports
                .Where(a => a.SportSanseID != null && mySanse.Contains(a.SportSanseID.Value) && a.IsActive == true)
                .Select(a => a.MemberID)
                .Distinct()
                .CountAsync();

            // کلاس‌های امروز
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
                    // ✅ فیکس CS0019: مقایسه رشته شمسی با string.Compare
                    RegValidCount = _db.Acc_MemberSports.Count(a => a.SportSanseID == s.SportSanseID && a.IsActive == true && string.Compare(a.EndDate, todayStr) >= 0)
                }
            ).ToListAsync();

            // حضور امروز هر کلاس
            var sanseIds = classes.Select(c => c.SportSanseID).Distinct().ToList();
            var today = DateTime.Now.Date;
            var attendanceMap = new Dictionary<int, int>();

            if (sanseIds.Count > 0)
            {
                attendanceMap = await (
                    from t in _db.ACC_Traffics
                    join a in _db.Acc_MemberSports on t.SportMemberID equals a.SportMemberID
                    where a.SportSanseID != null && sanseIds.Contains(a.SportSanseID.Value)
                          && t.EntryDateTime != null
                          && t.EntryDateTime.Value.Date == today
                    group t by (a.SportSanseID ?? 0) into g   // ✅ فیکس Warning CS8714
                    select new { SanseID = g.Key, Cnt = g.Count() }
                ).ToDictionaryAsync(x => x.SanseID, x => x.Cnt);
            }

            // وضعیت هر کلاس
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

        // ========== لیست همه کلاس‌های مربی ==========
        public async Task<List<CoachClassListDto>> GetMyClassesAsync(int coachMemberID)
        {
            var todayStr = _helper.GetToday();

            var classes = await (
                from s in _db.Gen_SportSanses
                where s.CoachMemberID == coachMemberID && s.IsActive == true
                orderby s.Gen_Sport_Category.SportName, s.SanseName
                select new CoachClassListDto
                {
                    SportSanseID = s.SportSanseID,
                    SportName = s.Gen_Sport_Category.SportName,
                    SanseName = s.SanseName,
                    ClassCapacity = s.ClassCapacity,
                    TotalStudents = _db.Acc_MemberSports.Count(a => a.SportSanseID == s.SportSanseID),
                    // ✅ فیکس CS0019
                    ActiveStudents = _db.Acc_MemberSports.Count(a => a.SportSanseID == s.SportSanseID && a.IsActive == true && string.Compare(a.EndDate, todayStr) >= 0)
                }
            ).ToListAsync();

            // برنامه هفتگی همه کلاس‌ها (یک کوئری جدا)
            var classIds = classes.Select(c => c.SportSanseID).ToList();
            var details = classIds.Count == 0
                ? new List<CoachSchedulePartDto>()
                : await (
                    from d in _db.Set<Gen_SportSanseDetail>()
                    join w in _db.Set<Gen_DayOfWeek>() on d.DayID equals w.DayID
                    where d.SportSanseID != null && classIds.Contains(d.SportSanseID.Value)
                          && (d.IsActive == true || d.IsActive == null)
                    orderby d.DayID, d.StartTime
                    select new CoachSchedulePartDto
                    {
                        SportSanseID = d.SportSanseID ?? 0,
                        DayName = w.DayName,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime
                    }
                ).ToListAsync();

            foreach (var c in classes)
            {
                c.ScheduleSummary = BuildScheduleSummary(details.Where(d => d.SportSanseID == c.SportSanseID).ToList());
                c.FreeNow = c.ClassCapacity.HasValue ? c.ClassCapacity.Value - c.ActiveStudents : (int?)null;
            }

            return classes;
        }

        // ========== شاگردان یک کلاس ==========
        public async Task<(string ClassName, List<CoachStudentDto> Students)> GetClassStudentsAsync(int coachMemberID, int sportSanseID)
        {
            // چک مالکیت: سانس باید مال همین مربی باشه
            var sanse = await _db.Gen_SportSanses
                .Where(s => s.SportSanseID == sportSanseID && s.CoachMemberID == coachMemberID && s.IsActive == true)
                .Select(s => new { SportName = s.Gen_Sport_Category.SportName, s.SanseName })
                .FirstOrDefaultAsync();

            if (sanse == null)
                return ("", new List<CoachStudentDto>());

            var todayStr = _helper.GetToday();

            var students = await (
                from a in _db.Acc_MemberSports
                where a.SportSanseID == sportSanseID && a.MemberID != null
                join m in _db.Gen_Members on a.MemberID.Value equals m.MemberID
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                orderby p.FullName
                select new CoachStudentDto
                {
                    SportMemberID = a.SportMemberID,
                    MemberID = m.MemberID,
                    FullName = p.FullName ?? "",
                    Mobile = p.Mobile,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    // ✅ فیکس CS1061: اسم درست ستون = SessionCount
                    TotalSessions = (int)(a.SessionCount ?? 0),
                    // ✅ فیکس CS1061: جلسات مصرف‌شده = تعداد ترددهای همین ثبت‌نام
                    UsedSessions = _db.ACC_Traffics.Count(t => t.SportMemberID == a.SportMemberID),
                    IsActive = a.IsActive == true
                }
            ).ToListAsync();

            foreach (var st in students)
            {
                st.RemainingSessions = Math.Max(0, st.TotalSessions - st.UsedSessions);

                if (st.IsActive && st.EndDate != null && string.Compare(st.EndDate, todayStr) >= 0 && st.RemainingSessions > 0)
                {
                    st.StatusLabel = "فعال";
                    st.StatusClass = "success";
                }
                else if (st.EndDate != null && string.Compare(st.EndDate, todayStr) < 0)
                {
                    st.StatusLabel = "منقضی";
                    st.StatusClass = "danger";
                }
                else if (st.RemainingSessions == 0)
                {
                    st.StatusLabel = "جلسات تمام";
                    st.StatusClass = "warning";
                }
                else
                {
                    st.StatusLabel = "غیرفعال";
                    st.StatusClass = "secondary";
                }
            }

            return ($"{sanse.SportName} - {sanse.SanseName}", students);
        }

        // ========== چک نقش مربی ==========
        public async Task<bool> IsCoachAsync(int memberID)
        {
            if (memberID == 0) return false;

            var roleID = await _db.Gen_Members
                .Where(m => m.MemberID == memberID)
                .Select(m => m.RoleID)
                .FirstOrDefaultAsync();

            return roleID == 2;
        }

        // ========== خلاصه برنامه هفتگی ==========
        private static string BuildScheduleSummary(List<CoachSchedulePartDto> details)
        {
            if (details == null || details.Count == 0)
                return "بدون برنامه";

            var dayShort = new Dictionary<string, string>
            {
                ["شنبه"] = "ش",
                ["یک‌شنبه"] = "ی",
                ["دوشنبه"] = "د",
                ["سه‌شنبه"] = "س",
                ["چهارشنبه"] = "چ",
                ["پنج‌شنبه"] = "پ",
                ["جمعه"] = "ج"
            };

            var parts = details
                .GroupBy(d => $"{d.StartTime:hh\\:mm} تا {d.EndTime:hh\\:mm}")
                .Select(g => string.Join(" و ", g.Select(x =>
                    dayShort.TryGetValue(x.DayName ?? "", out var s) ? s : x.DayName)) + " " + g.Key)
                .ToList();

            return string.Join(" | ", parts);
        }
        /// <summary>
        /// کارت کامل شاگرد: اطلاعات دوره + تاریخچه حضور
        /// </summary>
        public async Task<CoachStudentDetailsDto?> GetStudentDetailsAsync(int coachMemberID, long sportMemberID)
        {
            // چک مالکیت: ثبت‌نام باید مال یکی از کلاس‌های همین مربی باشه
            var reg = await (
                from a in _db.Acc_MemberSports
                join s in _db.Gen_SportSanses on a.SportSanseID equals s.SportSanseID
                where a.SportMemberID == sportMemberID && s.CoachMemberID == coachMemberID
                select new
                {
                    a.MemberID,
                    a.StartDate,
                    a.EndDate,
                    a.IsActive,
                    TotalSessions = (int)(a.SessionCount ?? 0),
                    ClassName = s.Gen_Sport_Category.SportName + " - " + s.SanseName
                }
            ).FirstOrDefaultAsync();

            if (reg == null || reg.MemberID == null)
                return null;

            var info = await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == reg.MemberID
                select new { p.FullName, p.Mobile }
            ).FirstOrDefaultAsync();

            var todayStr = _helper.GetToday();
            var used = await _db.ACC_Traffics.CountAsync(t => t.SportMemberID == sportMemberID);

            var dto = new CoachStudentDetailsDto
            {
                ClassName = reg.ClassName,
                MemberID = reg.MemberID.Value,
                FullName = info?.FullName ?? "",
                Mobile = info?.Mobile,
                StartDate = reg.StartDate,
                EndDate = reg.EndDate,
                TotalSessions = reg.TotalSessions,
                UsedSessions = used,
                RemainingSessions = Math.Max(0, reg.TotalSessions - used),
                IsActive = reg.IsActive == true
            };

            // وضعیت دوره
            if (dto.IsActive && dto.EndDate != null && string.Compare(dto.EndDate, todayStr) >= 0 && dto.RemainingSessions > 0)
            { dto.StatusLabel = "فعال"; dto.StatusClass = "success"; }
            else if (dto.EndDate != null && string.Compare(dto.EndDate, todayStr) < 0)
            { dto.StatusLabel = "منقضی"; dto.StatusClass = "danger"; }
            else if (dto.RemainingSessions == 0)
            { dto.StatusLabel = "جلسات تمام"; dto.StatusClass = "warning"; }
            else
            { dto.StatusLabel = "غیرفعال"; dto.StatusClass = "secondary"; }

            // تاریخچه حضور
            dto.Attendances = await _db.ACC_Traffics
                .Where(t => t.SportMemberID == sportMemberID)
                .OrderByDescending(t => t.TrafficID)
                .Select(t => new CoachAttendanceDto
                {
                    TrafficID = t.TrafficID,
                    EntryDate = t.EntryDate,
                    EntryTime = t.EntryTime,
                    ExitDate = t.ExitDate,
                    ExitTime = t.ExitTime,
                    IsOpen = t.ExitDate == null && t.ExitDateTime == null
                })
                .ToListAsync();

            return dto;
        }
        /// <summary>
        /// گزارش پورسانت مربی (با فیلتر بازه زمانی)
        /// </summary>
        public async Task<CoachCommissionSummaryDto> GetCommissionReportAsync(int coachMemberID, string? fromDate = null, string? toDate = null)
        {
            var todayStr = _helper.GetToday();

            // پیش‌فرض: ماه جاری (۳۰ روز اخیر)
            // ✅ درست: تبدیل مستقیم با PersianCalendar
            if (string.IsNullOrEmpty(fromDate))
            {
                var pc = new System.Globalization.PersianCalendar();
                var d = DateTime.Now.AddDays(-30);
                fromDate = $"{pc.GetYear(d):D4}/{pc.GetMonth(d):D2}/{pc.GetDayOfMonth(d):D2}";
            }
            if (string.IsNullOrEmpty(toDate))
                toDate = todayStr;

            var rows = await (
                from a in _db.Acc_MemberSports
                join s in _db.Gen_SportSanses on a.SportSanseID equals s.SportSanseID
                join m in _db.Gen_Members on a.MemberID equals m.MemberID
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where s.CoachMemberID == coachMemberID
                      && a.CreationDate != null
                      && string.Compare(a.CreationDate, fromDate) >= 0
                      && string.Compare(a.CreationDate, toDate) <= 0
                orderby a.CreationDate descending
                select new CoachCommissionRowDto
                {
                    SportMemberID = a.SportMemberID,
                    StudentName = p.FullName ?? "",
                    Mobile = p.Mobile,
                    SportName = s.Gen_Sport_Category.SportName,
                    SanseName = s.SanseName,
                    StartDate = a.StartDate,
                    Amount = a.FinalPayment ?? 0,
                    CoachAmount = a.CoachAmount ?? 0,
                    CoachRevivalAmount = a.CoachRevivalAmount ?? 0,
                    IsRevival = a.IsRevival == true
                }
            ).ToListAsync();

            // محاسبه ماه جاری (همین ماه شمسی)
            var thisMonthPrefix = todayStr.Substring(0, 7); // "1405/06"
            var thisMonthRows = rows.Where(r => r.StartDate != null && r.StartDate.StartsWith(thisMonthPrefix)).ToList();

            return new CoachCommissionSummaryDto
            {
                FromDate = fromDate,     
                ToDate = toDate,
                ThisMonthAmount = thisMonthRows.Sum(r => r.TotalCommission),
                ThisMonthCount = thisMonthRows.Count,
                TotalAmount = rows.Sum(r => r.TotalCommission),
                TotalCount = rows.Count,
                Rows = rows
            };
        }
        /// <summary>
        /// لیست شاگردان با آخرین پیام‌ها (برای مربی)
        /// </summary>
        public async Task<List<CoachMessageSummaryDto>> GetMessageSummariesAsync(int coachMemberID)
        {
            // شاگردان فعلی مربی
            var mySanseIds = await _db.Gen_SportSanses
                .Where(s => s.CoachMemberID == coachMemberID && s.IsActive == true)
                .Select(s => s.SportSanseID)
                .ToListAsync();

            if (mySanseIds.Count == 0)
                return new List<CoachMessageSummaryDto>();

            var students = await (
                from a in _db.Acc_MemberSports             
                where a.SportSanseID != null && mySanseIds.Contains(a.SportSanseID.Value) && a.IsActive == true && a.MemberID != null
                join m in _db.Gen_Members on a.MemberID.Value equals m.MemberID
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                group m by new { m.MemberID, p.FullName, p.Mobile } into g
                select new
                {
                    g.Key.MemberID,
                    g.Key.FullName,
                    g.Key.Mobile
                }
            ).ToListAsync();

            var result = new List<CoachMessageSummaryDto>();

            foreach (var st in students)
            {
                // پیام‌های بین مربی و این شاگرد
                var messages = await _db.MsgMessages
                    .Where(m => m.IsActive &&
                                ((m.SenderMemberID == coachMemberID && m.TargetMemberID == st.MemberID) ||
                                 (m.SenderMemberID == st.MemberID && m.TargetMemberID == coachMemberID)))
                    .OrderByDescending(m => m.CreationDateTime)
                    .FirstOrDefaultAsync();

                // تعداد خوانده‌نشده (پیام‌های شاگرد به مربی)
                var unread = await _db.MsgMessages
                    .CountAsync(m => m.IsActive &&
                                     m.SenderMemberID == st.MemberID &&
                                     m.TargetMemberID == coachMemberID &&
                                     !_db.MsgReads.Any(r => r.MessageID == m.MessageID && r.MemberID == coachMemberID));

                result.Add(new CoachMessageSummaryDto
                {
                    StudentMemberID = st.MemberID,
                    StudentName = st.FullName ?? "",
                    Mobile = st.Mobile,
                    LastMessage = messages?.Body,
                    LastMessageDate = messages?.CreationDate,
                    LastMessageTime = messages?.CreationTime,
                    UnreadCount = unread
                });
            }

            return result.OrderByDescending(r => r.LastMessageDate).ThenByDescending(r => r.LastMessageTime).ToList();
        }

        /// <summary>
        /// چت با یک شاگرد
        /// </summary>
        public async Task<CoachChatDto?> GetChatAsync(int coachMemberID, int studentMemberID)
        {
            // چک مالکیت: شاگرد باید در یکی از کلاس‌های مربی باشه
            var mySanseIds = await _db.Gen_SportSanses
                .Where(s => s.CoachMemberID == coachMemberID && s.IsActive == true)
                .Select(s => s.SportSanseID)
                .ToListAsync();

            var isValidStudent = await _db.Acc_MemberSports
                .AnyAsync(a => a.SportSanseID !=null && mySanseIds.Contains(a.SportSanseID.Value) && a.MemberID == studentMemberID && a.IsActive == true);

            if (!isValidStudent)
                return null;

            var studentName = await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == studentMemberID
                select p.FullName
            ).FirstOrDefaultAsync() ?? "";

            var messages = await _db.MsgMessages
                .Where(m => m.IsActive &&
                            ((m.SenderMemberID == coachMemberID && m.TargetMemberID == studentMemberID) ||
                             (m.SenderMemberID == studentMemberID && m.TargetMemberID == coachMemberID)))
                .OrderBy(m => m.CreationDateTime)
                .Select(m => new CoachMessageItemDto
                {
                    MessageID = m.MessageID,
                    IsFromMe = m.SenderMemberID == coachMemberID,
                    Title = m.Title ?? "",
                    Body = m.Body,
                    CreationDate = m.CreationDate ?? "",
                    CreationTime = m.CreationTime ?? ""
                })
                .ToListAsync();

            // علامت‌گذاری پیام‌های خوانده‌نشده به عنوان خوانده‌شده
            var unreadIds = await _db.MsgMessages
                .Where(m => m.IsActive &&
                            m.SenderMemberID == studentMemberID &&
                            m.TargetMemberID == coachMemberID &&
                            !_db.MsgReads.Any(r => r.MessageID == m.MessageID && r.MemberID == coachMemberID))
                .Select(m => m.MessageID)
                .ToListAsync();

            if (unreadIds.Count > 0)
            {
                foreach (var id in unreadIds)
                {
                    _db.MsgReads.Add(new Msg_Read
                    {
                        MessageID = id,
                        MemberID = coachMemberID,
                        ReadDateTime = DateTime.Now
                    });
                }
                await _db.SaveChangesAsync();
            }

            return new CoachChatDto
            {
                StudentMemberID = studentMemberID,
                StudentName = studentName,
                Messages = messages
            };
        }

        /// <summary>
        /// ارسال پیام از مربی به شاگرد
        /// </summary>
        public async Task<bool> SendMessageToStudentAsync(int coachMemberID, int studentMemberID, string? title, string body)
        {
            // چک مالکیت
            var mySanseIds = await _db.Gen_SportSanses
                .Where(s => s.CoachMemberID == coachMemberID && s.IsActive == true)
                .Select(s => s.SportSanseID)
                .ToListAsync();

            var isValid = await _db.Acc_MemberSports
                .AnyAsync(a => a.SportSanseID !=null && mySanseIds.Contains(a.SportSanseID.Value) && a.MemberID == studentMemberID);

            if (!isValid)
                return false;

            var pc = new System.Globalization.PersianCalendar();
            var now = DateTime.Now;
            var date = $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00}";
            var time = now.ToString("HH:mm:ss");

            _db.MsgMessages.Add(new Msg_Message
            {
                Title = title,
                Body = body,
                TargetType = 4,  // پیام به عضو خاص
                TargetMemberID = studentMemberID,
                SenderMemberID = coachMemberID,
                IsActive = true,
                CreationDateTime = now,
                CreationDate = date,
                CreationTime = time
            });

            await _db.SaveChangesAsync();
            return true;
        }
        /// <summary>
        /// تعداد پیام‌های خوانده‌نشده مربی (پیام‌های شاگردان به مربی)
        /// </summary>
        public async Task<int> GetCoachUnreadCountAsync(int coachMemberID)
        {
            return await _db.MsgMessages
                .Where(m => m.IsActive &&
                            m.TargetType == 4 &&
                            m.TargetMemberID == coachMemberID &&
                            m.SenderMemberID != null &&
                            !_db.MsgReads.Any(r => r.MessageID == m.MessageID && r.MemberID == coachMemberID))
                .CountAsync();
        }

    }
}