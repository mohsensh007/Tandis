using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// گزارش‌های مدیریتی — همه کوئری‌ها AsNoTracking (خواندنی)
    /// </summary>
    public class AdminReportService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<AdminReportService> _logger;

        public AdminReportService(FullSportDbContext db, CommonHelperService helper, ILogger<AdminReportService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        // ============================================================
        //  گزارش ترددها (خواندنی)
        // ============================================================
        public async Task<AdminReportResponse<AdminTrafficRowDto, TrafficReportSummaryDto>> GetTrafficReportAsync(short shiftID, string? from, string? to)
        {
            var query = _db.ACC_Traffics
                .AsNoTracking()
                .Where(t => t.ShiftID == shiftID);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(to) <= 0);

            var rawRows = await (
                 from t in query
                 join m in _db.Gen_Members on t.MemberID equals m.MemberID into memJoin
                 from m in memJoin.DefaultIfEmpty()
                 join p in _db.Gen_Persons on m.PersonID equals p.PersonID into personJoin
                 from p in personJoin.DefaultIfEmpty()
                 orderby t.TrafficID descending
                 select new
                 {
                     t.TrafficID,
                     t.PersonName,
                     t.EntryDate,
                     t.EntryTime,
                     t.ExitDate,
                     t.ExitTime,
                     t.EntryDesc,
                     t.IsGuest,
                     t.BoxID,
                     MemberFullName = p != null ? p.FullName : string.Empty,
                     MemberID = m != null ? m.MemberID : 0
                 }).ToListAsync();

            var rows = rawRows.Select(x => new AdminTrafficRowDto
            {
                TrafficID = x.TrafficID,
                PersonName = x.IsGuest == true ? x.PersonName : (x.MemberFullName ?? x.PersonName ?? "-"),
                MemberCode = (x.MemberID > 0) ? _helper.SetSeprator(x.MemberID) : null,
                EntryDate = x.EntryDate,
                EntryTime = x.EntryTime,
                ExitDate = x.ExitDate,
                ExitTime = x.ExitTime,
                EntryDesc = x.EntryDesc,
                IsGuest = x.IsGuest,
                BoxID = x.BoxID,
            }).ToList();

            var summary = new TrafficReportSummaryDto
            {
                TotalCount = rows.Count,
                MemberCount = rows.Count(r => r.IsGuest != true),
                GuestCount = rows.Count(r => r.IsGuest == true)
            };

            return new AdminReportResponse<AdminTrafficRowDto, TrafficReportSummaryDto>
            {
                Data = rows,
                Summary = summary
            };
        }

        // ============================================================
        //  گزارش ثبت‌نام و تمدید (خواندنی)
        // ============================================================
        public async Task<AdminReportResponse<AdminRegisterRowDto, RegisterReportSummaryDto>> GetRegisterReportAsync(short shiftID, string? from, string? to, string mode)
        {
            var query = _db.Acc_MemberSports
                .AsNoTracking()
                .Where(ms => ms.Gen_SportSanse != null && ms.Gen_SportSanse.ShiftID == shiftID);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(ms => ms.CreationDate != null && ms.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(ms => ms.CreationDate != null && ms.CreationDate.CompareTo(to) <= 0);

            if (mode == "register")
                query = query.Where(ms => ms.IsRevival == null || ms.IsRevival == false);
            else if (mode == "renew")
                query = query.Where(ms => ms.IsRevival == true);

            var rawRows = await (
                from ms in query
                join sanse in _db.Gen_SportSanses on ms.SportSanseID equals sanse.SportSanseID into sj
                from sanse in sj.DefaultIfEmpty()
                join sportCat in _db.Gen_Sport_Categories on sanse.SportCatID equals sportCat.SportCatID into sportCatJoin
                from sportCat in sportCatJoin.DefaultIfEmpty()
                join coachM in _db.Gen_Members on sanse.CoachMemberID equals coachM.MemberID into coachMJoin
                from coachM in coachMJoin.DefaultIfEmpty()
                join coachP in _db.Gen_Persons on coachM.PersonID equals coachP.PersonID into coachPJoin
                from coachP in coachPJoin.DefaultIfEmpty()
                join memType in _db.Gen_MembershipTypes on sanse.MembershipTypeID equals memType.MembershipTypeID into memTypeJoin
                from memType in memTypeJoin.DefaultIfEmpty()
                join period in _db.Gen_Periods on sanse.PeriodID equals period.PeriodID into periodJoin
                from period in periodJoin.DefaultIfEmpty()
                orderby ms.SportMemberID descending
                select new
                {
                    ms.SportMemberID,
                    ms.MemberID,
                    ms.IsRevival,
                    ms.FinalPayment,
                    ms.StartDate,
                    ms.EndDate,
                    ms.CreationDate,
                    ms.CreationTime,
                    SportName = sportCat != null ? sportCat.SportName : "",
                    SanseName = sanse != null ? sanse.SanseName : "",
                    CoachName = coachP != null ? coachP.FullName : "",
                    MembershipTypeDesc = memType != null ? memType.MembershipTypeDesc : "",
                    PeriodDesc = period != null ? period.Description : ""
                }).ToListAsync();

            var memberIDs = rawRows
                .Where(x => x.MemberID.HasValue && x.MemberID.Value > 0)
                .Select(x => x.MemberID.GetValueOrDefault())
                .Distinct()
                .ToList();

            var memberInfos = new Dictionary<int, (string FullName, string MemberCode)>();
            if (memberIDs.Count > 0)
            {
                memberInfos = await (
                    from m in _db.Gen_Members.AsNoTracking()
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where memberIDs.Contains(m.MemberID)
                    select new { m.MemberID, p.FullName }
                )
                .ToDictionaryAsync(
                    x => x.MemberID,
                    x => (x.FullName ?? "", _helper.SetSeprator(x.MemberID))
                );
            }

            var result = new List<AdminRegisterRowDto>();
            long totalAmount = 0;
            int registerCount = 0;
            int renewalCount = 0;

            foreach (var item in rawRows)
            {
                var personName = "";
                string? memberCode = null;

                if (item.MemberID > 0 && memberInfos.TryGetValue(item.MemberID.Value, out var info))
                {
                    personName = info.FullName;
                    memberCode = info.MemberCode;
                }

                bool isRevival = item.IsRevival ?? false;
                if (isRevival) renewalCount++; else registerCount++;
                totalAmount += item.FinalPayment ?? 0;

                result.Add(new AdminRegisterRowDto
                {
                    SportMemberID = item.SportMemberID,
                    PersonName = personName,
                    MemberCode = memberCode,
                    SportName = item.SportName ?? "",
                    SanseName = item.SanseName ?? "",
                    CoachName = item.CoachName ?? "",
                    MembershipTypeDesc = item.MembershipTypeDesc ?? "",
                    PeriodDesc = item.PeriodDesc ?? "",
                    FinalPayment = item.FinalPayment ?? 0,
                    FinalPaymentDisplay = _helper.SetSeprator(item.FinalPayment ?? 0) + " ریال",
                    StartDate = item.StartDate,
                    EndDate = item.EndDate,
                    CreationDate = item.CreationDate,
                    CreationTime = item.CreationTime,
                    IsRevival = isRevival
                });
            }

            var summary = new RegisterReportSummaryDto
            {
                TotalCount = result.Count,
                RegisterCount = registerCount,
                RenewalCount = renewalCount,
                TotalAmount = totalAmount,
                TotalAmountDisplay = _helper.SetSeprator(totalAmount) + " ریال"
            };

            return new AdminReportResponse<AdminRegisterRowDto, RegisterReportSummaryDto>
            {
                Data = result,
                Summary = summary
            };
        }

        // ============================================================
        //  گزارش تک‌جلسه‌ها (خواندنی)
        // ============================================================
        public async Task<AdminReportResponse<AdminOneSessionRowDto, OneSessionReportSummaryDto>> GetOneSessionReportAsync(short shiftID, string? from, string? to)
        {
            var query = _db.ACC_Tickets
                .AsNoTracking()
                .Where(t => t.ShiftID == shiftID);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(t => t.CreationDate != null && t.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(t => t.CreationDate != null && t.CreationDate.CompareTo(to) <= 0);

            var rows = await (
                from t in query
                orderby t.TicketID descending
                select new { t, tarefe = t.Gen_Tarefe, san = t.Gen_Tarefe != null ? t.Gen_Tarefe.Gen_San : null }
            ).ToListAsync();

            var result = new List<AdminOneSessionRowDto>();
            long totalAmount = 0;

            foreach (var item in rows)
            {
                totalAmount += item.t.Amount ?? 0;
                result.Add(new AdminOneSessionRowDto
                {
                    TicketID = item.t.TicketID,
                    PersonName = item.t.FullName,
                    SansName = item.san?.Sans ?? "",
                    TarefeName = item.tarefe?.Tarefe ?? "",
                    Amount = item.t.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(item.t.Amount ?? 0) + " ریال",
                    CreationDate = item.t.CreationDate,
                    CreationTime = item.t.CreationTime != null
                        ? item.t.CreationTime.Value.ToString(@"hh\:mm\:ss")
                        : "",
                    TicketDesc = item.t.TicketDesc
                });
            }

            var summary = new OneSessionReportSummaryDto
            {
                TotalCount = result.Count,
                TotalAmount = totalAmount,
                TotalAmountDisplay = _helper.SetSeprator(totalAmount) + " ریال"
            };

            return new AdminReportResponse<AdminOneSessionRowDto, OneSessionReportSummaryDto>
            {
                Data = result,
                Summary = summary
            };
        }

        // ============================================================
        //  گزارش صندوق (خواندنی)
        // ============================================================
        public async Task<AdminReportResponse<AdminFinanceRowDto, FinanceReportSummaryDto>> GetFinanceReportAsync(short shiftID, string? from, string? to)
        {
            var result = new List<AdminFinanceRowDto>();

            var creditQuery = _db.Cash_CreditStatments.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(from))
                creditQuery = creditQuery.Where(c => c.CreationDate != null && c.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                creditQuery = creditQuery.Where(c => c.CreationDate != null && c.CreationDate.CompareTo(to) <= 0);

            var credits = await (
                from c in creditQuery
                join m in _db.Gen_Members.Where(m => m.ShiftID == shiftID) on c.MemberID equals m.MemberID
                orderby c.CreditID descending
                select c
            ).ToListAsync();

            long totalCredit = 0;
            foreach (var c in credits)
            {
                var personName = await GetPersonNameAsync(c.MemberID ?? 0);
                totalCredit += c.Amount ?? 0;

                result.Add(new AdminFinanceRowDto
                {
                    RowID = c.CreditID,
                    RowType = "دریافتی",
                    TypeDesc = GetCreditTypeDesc(c.CreditTypeID),
                    Amount = c.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(c.Amount ?? 0) + " ریال",
                    Description = c.CreditDesc,
                    PersonName = personName,
                    DateDisplay = c.CreationDate
                });
            }

            var finalResult = result.OrderByDescending(r => r.DateDisplay).ToList();

            var summary = new FinanceReportSummaryDto
            {
                TotalCount = finalResult.Count,
                CreditCount = credits.Count,
                DebitCount = 0,
                TotalCredit = totalCredit,
                TotalDebit = 0,
                Balance = totalCredit,
                TotalCreditDisplay = _helper.SetSeprator(totalCredit) + " ریال",
                TotalDebitDisplay = "۰ ریال",
                BalanceDisplay = _helper.SetSeprator(totalCredit) + " ریال"
            };

            return new AdminReportResponse<AdminFinanceRowDto, FinanceReportSummaryDto>
            {
                Data = finalResult,
                Summary = summary
            };
        }

        // ============================================================
        //  متدهای کمکی
        // ============================================================

        private async Task<string?> GetPersonNameAsync(int memberID)
        {
            if (memberID <= 0) return null;
            return await (
                from m in _db.Gen_Members.AsNoTracking()
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == memberID
                select p.FullName
            ).FirstOrDefaultAsync();
        }

        private static string GetCreditTypeDesc(byte? typeId)
        {
            return typeId switch
            {
                1 => "شهریه دوره",
                2 => "خرید از فروشگاه",
                3 => "خرید خدمات",
                10 => "جلسه آزاد",
                11 => "اعتبار ریالی",
                _ => $"نوع {typeId}"
            };
        }

        private static string GetDebitTypeDesc(byte? typeId)
        {
            return typeId switch
            {
                1 => "شهریه دوره",
                2 => "خرید از فروشگاه",
                3 => "خرید خدمات",
                7 => "مالیات",
                10 => "جلسه آزاد",
                11 => "استفاده از اعتبار",
                _ => $"نوع {typeId}"
            };
        }

        // ============================================================
        //  آمار داشبورد مدیریت (خواندنی)
        // ============================================================
        public async Task<DashboardStatsResult> GetDashboardStatsAsync(short shiftID, string period)
        {
            var pc = new System.Globalization.PersianCalendar();
            string ToShamsi(DateTime d) => $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";

            var today = DateTime.Now.Date;
            DateTime curFrom, curTo, prevFrom, prevTo;
            string curTitle, prevTitle;

            if (period == "week")
            {
                curFrom = today.AddDays(-6); curTo = today;
                prevFrom = today.AddDays(-13); prevTo = today.AddDays(-7);
                curTitle = "هفته جاری"; prevTitle = "هفته قبل";
            }
            else if (period == "month")
            {
                curFrom = today.AddDays(-29); curTo = today;
                prevFrom = today.AddDays(-59); prevTo = today.AddDays(-30);
                curTitle = "ماه جاری"; prevTitle = "ماه قبل";
            }
            else
            {
                curFrom = today; curTo = today;
                prevFrom = today.AddDays(-1); prevTo = today.AddDays(-1);
                curTitle = "امروز"; prevTitle = "دیروز";
            }

            var curFromS = ToShamsi(curFrom);
            var curToS = ToShamsi(curTo);
            var prevFromS = ToShamsi(prevFrom);
            var prevToS = ToShamsi(prevTo);

            string curLabel, prevLabel;
            if (period == "day")
            {
                curLabel = $"{curTitle}: {curFromS}";
                prevLabel = $"{prevTitle}: {prevFromS}";
            }
            else
            {
                curLabel = $"{curTitle}: از {curFromS} تا {curToS}";
                prevLabel = $"{prevTitle}: از {prevFromS} تا {prevToS}";
            }

            var cur = await CountRegisters(shiftID, curFromS, curToS);
            var prev = await CountRegisters(shiftID, prevFromS, prevToS);

            var curCredit = await SumCredits(shiftID, curFromS, curToS);
            var prevCredit = await SumCredits(shiftID, prevFromS, prevToS);

            var todayS = ToShamsi(today);
            var insideRows = await _db.ACC_Traffics
                .AsNoTracking()
                .Where(t => t.ShiftID == shiftID && t.EntryDate == todayS
                          && (t.TrafficStatus == 1 || t.TrafficStatus == 100))
                .Select(t => t.MemberID)
                .ToListAsync();
            var insideCount = insideRows.Count(m => m == null)
                            + insideRows.Where(m => m != null).Distinct().Count();

            return new DashboardStatsResult
            {
                InsideCount = insideCount,
                CurrentRegister = cur.Item1,
                CurrentRenew = cur.Item2,
                PreviousRegister = prev.Item1,
                PreviousRenew = prev.Item2,
                CurrentCredit = curCredit.sum,
                CurrentCreditCount = curCredit.count,
                PreviousCredit = prevCredit.sum,
                PreviousCreditCount = prevCredit.count,
                CurrentLabel = curLabel,
                PreviousLabel = prevLabel
            };
        }

        private async Task<(long sum, int count)> SumCredits(short shiftID, string fromDate, string toDate)
        {
            var amounts = await (
                from c in _db.Cash_CreditStatments.AsNoTracking()
                where c.CreationDate != null
                   && c.CreationDate.CompareTo(fromDate) >= 0
                   && c.CreationDate.CompareTo(toDate) <= 0
                join m in _db.Gen_Members on c.MemberID equals m.MemberID into mj
                from m in mj.DefaultIfEmpty()
                join t in _db.ACC_Traffics on c.TrafficID equals t.TrafficID into tj
                from t in tj.DefaultIfEmpty()
                where m.ShiftID == shiftID || t.ShiftID == shiftID
                select c.Amount)
                .ToListAsync();

            var filtered = amounts.Where(a => a.HasValue).Select(a => a!.Value).ToList();
            return (filtered.Sum(), filtered.Count);
        }

        private async Task<(int, int)> CountRegisters(short shiftID, string from, string to)
        {
            var flags = await _db.Acc_MemberSports
                .AsNoTracking()
                .Where(ms => ms.Gen_SportSanse != null && ms.Gen_SportSanse.ShiftID == shiftID
                          && ms.CreationDate != null
                          && ms.CreationDate.CompareTo(from) >= 0
                          && ms.CreationDate.CompareTo(to) <= 0)
                .Select(ms => ms.IsRevival)
                .ToListAsync();

            var renew = flags.Count(f => f == true);
            return (flags.Count - renew, renew);
        }

        public async Task<List<AdminInsideRowDto>> GetInsideListAsync(short shiftID)
        {
            var pc = new System.Globalization.PersianCalendar();
            var now = DateTime.Now;
            var todayS = $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00}";

            var raw = await (
                from t in _db.ACC_Traffics.AsNoTracking()
                where t.ShiftID == shiftID && t.EntryDate == todayS
                   && (t.TrafficStatus == 1 || t.TrafficStatus == 100)
                join m in _db.Gen_Members on t.MemberID equals m.MemberID into memJoin
                from m in memJoin.DefaultIfEmpty()
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID into personJoin
                from p in personJoin.DefaultIfEmpty()
                join ms in _db.Acc_MemberSports on t.SportMemberID equals ms.SportMemberID into msJoin
                from ms in msJoin.DefaultIfEmpty()
                orderby t.EntryDateTime descending
                select new
                {
                    t.TrafficID,
                    t.MemberID,
                    t.PersonName,
                    t.EntryTime,
                    t.IsGuest,
                    FullName = p != null ? p.FullName : "",
                    SportName = ms != null && ms.Gen_SportSanse != null
                                ? (ms.Gen_SportSanse.Gen_Sport_Category != null ? ms.Gen_SportSanse.Gen_Sport_Category.SportName + " " : "") + (ms.Gen_SportSanse.SanseName ?? "")
                                : ""
                })
                .ToListAsync();

            return raw.Select(x => new AdminInsideRowDto
            {
                TrafficID = x.TrafficID,
                PersonName = x.MemberID != null ? (string.IsNullOrEmpty(x.FullName) ? "-" : x.FullName)
                                                : (string.IsNullOrEmpty(x.PersonName) ? "مهمان" : x.PersonName),
                MemberCode = x.MemberID != null && x.MemberID > 0 ? _helper.SetSeprator(x.MemberID.Value) : null,
                EntryTime = (x.EntryTime ?? "").Trim(),
                SportName = string.IsNullOrEmpty(x.SportName) ? "-" : x.SportName,
                IsGuest = x.MemberID == null || x.IsGuest == true
            }).ToList();
        }

        /// <summary>گزارش نظارت پیام‌ها (خواندنی)</summary>
        public async Task<List<AdminCoachMessageRowDto>> GetCoachStudentMessagesReportAsync(short shiftID, string? from, string? to, int? coachMemberID = null)
        {
            var query = _db.MsgMessages
                .AsNoTracking()
                .Where(m => m.IsActive && m.TargetType == 4 && m.SenderMemberID != null && m.TargetMemberID != null);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(m => m.CreationDate != null && m.CreationDate.CompareTo(from) >= 0);

            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(m => m.CreationDate != null && m.CreationDate.CompareTo(to) <= 0);

            if (coachMemberID.HasValue && coachMemberID.Value > 0)
            {
                var cid = coachMemberID.Value;
                query = query.Where(m => m.SenderMemberID == cid || m.TargetMemberID == cid);
            }

            return await (
                from m in query
                join sm in _db.Gen_Members on m.SenderMemberID equals sm.MemberID
                join tm in _db.Gen_Members on m.TargetMemberID equals tm.MemberID
                join sp in _db.Gen_Persons on sm.PersonID equals sp.PersonID
                join tp in _db.Gen_Persons on tm.PersonID equals tp.PersonID
                where (sm.RoleID == 2 || tm.RoleID == 2)
                      && (sm.ShiftID == shiftID || tm.ShiftID == shiftID)
                orderby m.CreationDateTime descending
                select new AdminCoachMessageRowDto
                {
                    MessageID = m.MessageID,
                    CoachName = sm.RoleID == 2 ? (sp.FullName ?? "-") : (tp.FullName ?? "-"),
                    StudentName = sm.RoleID == 2 ? (tp.FullName ?? "-") : (sp.FullName ?? "-"),
                    Title = m.Title,
                    Body = m.Body,
                    CreationDate = m.CreationDate ?? "",
                    CreationTime = m.CreationTime ?? ""
                }
            ).ToListAsync();
        }
    }
}