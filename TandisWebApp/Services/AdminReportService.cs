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
        //  گزارش فروش بلیت (خواندنی)
        // ============================================================
        public async Task<AdminReportResponse<AdminTicketSaleRowDto, SalesReportSummaryDto>> GetTicketSalesReportAsync(short shiftID, string? from, string? to)
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
                orderby t.CreationDate descending, t.TicketID descending
                select new
                {
                    t.TicketID,
                    t.FullName,
                    t.Amount,
                    t.IsPos,
                    t.CreationDate,
                    t.CreationTime,
                    t.TicketDesc,
                    TarefeName = t.Gen_Tarefe != null ? t.Gen_Tarefe.Tarefe : null,
                    SansName = t.Gen_San != null ? t.Gen_San.Sans : null
                }).ToListAsync();

            var result = new List<AdminTicketSaleRowDto>();
            long total = 0;
            foreach (var x in rows)
            {
                total += x.Amount ?? 0;
                result.Add(new AdminTicketSaleRowDto
                {
                    TicketID = x.TicketID,
                    PersonName = x.FullName,
                    SansName = x.SansName,
                    TarefeName = x.TarefeName,
                    Amount = x.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(x.Amount ?? 0) + " ریال",
                    CreationDate = x.CreationDate,
                    CreationTime = x.CreationTime.HasValue ? x.CreationTime.Value.ToString("HH:mm:ss") : "",
                    TicketDesc = x.TicketDesc,
                    IsPos = x.IsPos
                });
            }

            return new AdminReportResponse<AdminTicketSaleRowDto, SalesReportSummaryDto>
            {
                Data = result,
                Summary = new SalesReportSummaryDto
                {
                    TotalCount = result.Count,
                    TotalAmount = total,
                    TotalAmountDisplay = _helper.SetSeprator(total) + " ریال"
                }
            };
        }

        // ============================================================
        //  گزارش فروش خدمات (خواندنی)
        // ============================================================
        public async Task<AdminReportResponse<AdminServiceSaleRowDto, SalesReportSummaryDto>> GetServicesReportAsync(short shiftID, string? from, string? to)
        {
            var query = _db.ACC_MemberServices
                .AsNoTracking()
                .Where(s => s.ShiftID == shiftID);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(s => s.CreationDate != null && s.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(s => s.CreationDate != null && s.CreationDate.CompareTo(to) <= 0);

            var rows = await (
                from s in query
                join tr in _db.ACC_Traffics on s.TrafficID equals (long?)tr.TrafficID into trj
                from tr in trj.DefaultIfEmpty()
                join m in _db.Gen_Members on tr.MemberID equals m.MemberID into mj
                from m in mj.DefaultIfEmpty()
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID into pj
                from p in pj.DefaultIfEmpty()
                orderby s.CreationDate descending, s.MemberServiceID descending
                select new
                {
                    s.MemberServiceID,
                    s.ServiceDesc,
                    s.ServiceAmount,
                    s.CreationDate,
                    s.CreationTime,
                    ServiceName = s.Gen_Service != null ? s.Gen_Service.ServiceDesc : null,
                    TrafficPersonName = tr != null ? tr.PersonName : null,
                    IsGuest = tr != null ? tr.IsGuest : null,
                    FullName = p != null ? p.FullName : null,
                    MemberID = m != null ? m.MemberID : 0
                }).ToListAsync();

            var result = new List<AdminServiceSaleRowDto>();
            long total = 0;
            foreach (var x in rows)
            {
                total += x.ServiceAmount ?? 0;

                var personName = !string.IsNullOrEmpty(x.FullName) ? x.FullName
                    : (!string.IsNullOrEmpty(x.TrafficPersonName) ? x.TrafficPersonName
                    : (x.IsGuest == true || x.MemberID == 0 ? "مهمان" : "-"));

                result.Add(new AdminServiceSaleRowDto
                {
                    MemberServiceID = x.MemberServiceID,
                    ServiceName = string.IsNullOrWhiteSpace(x.ServiceName) ? (x.ServiceDesc ?? "-") : x.ServiceName,
                    ServiceDesc = x.ServiceDesc,
                    PersonName = personName,
                    MemberCode = x.MemberID > 0 ? _helper.SetSeprator(x.MemberID) : null,
                    Amount = x.ServiceAmount ?? 0,
                    AmountDisplay = _helper.SetSeprator(x.ServiceAmount ?? 0) + " ریال",
                    CreationDate = x.CreationDate,
                    CreationTime = (x.CreationTime ?? "").Trim()
                });
            }

            return new AdminReportResponse<AdminServiceSaleRowDto, SalesReportSummaryDto>
            {
                Data = result,
                Summary = new SalesReportSummaryDto
                {
                    TotalCount = result.Count,
                    TotalAmount = total,
                    TotalAmountDisplay = _helper.SetSeprator(total) + " ریال"
                }
            };
        }

        // ============================================================
        //  گزارش تک‌جلسه (مهمان‌ها - خواندنی)
        //  افرادی که به‌عنوان مهمان آمده‌اند و سانس تک‌جلسه برایشان ثبت شده
        // ============================================================
        public async Task<AdminReportResponse<AdminGuestSessionRowDto, SalesReportSummaryDto>> GetGuestSessionReportAsync(short shiftID, string? from, string? to)
        {
            var query = _db.ACC_Traffics
                .AsNoTracking()
                .Where(t => t.ShiftID == shiftID && (t.IsGuest == true || t.MemberID == null));

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(to) <= 0);

            var rows = await (
                from t in query
                join s in _db.Gen_SportSanses on t.FreeSportSansID equals (int?)s.SportSanseID into sj
                from s in sj.DefaultIfEmpty()
                join m in _db.Gen_Members on t.MemberID equals m.MemberID into mj
                from m in mj.DefaultIfEmpty()
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID into pj
                from p in pj.DefaultIfEmpty()
                orderby t.EntryDate descending, t.TrafficID descending
                select new
                {
                    t.TrafficID,
                    t.PersonName,
                    t.Amount,
                    t.EntryDate,
                    t.EntryTime,
                    FullName = p != null ? p.FullName : null,
                    MemberID = m != null ? m.MemberID : 0,
                    SansName = s != null
                        ? ((s.Gen_Sport_Category != null ? s.Gen_Sport_Category.SportName + " - " : "") + (s.SanseName ?? ""))
                        : null
                }).ToListAsync();

            var result = new List<AdminGuestSessionRowDto>();
            long total = 0;
            foreach (var x in rows)
            {
                total += (long)(x.Amount ?? 0);

                var personName = !string.IsNullOrEmpty(x.FullName) ? x.FullName
                    : (!string.IsNullOrEmpty(x.PersonName) ? x.PersonName : "مهمان");

                result.Add(new AdminGuestSessionRowDto
                {
                    TrafficID = x.TrafficID,
                    PersonName = personName,
                    MemberCode = x.MemberID > 0 ? _helper.SetSeprator(x.MemberID) : null,
                    SansName = string.IsNullOrWhiteSpace(x.SansName) ? "-" : x.SansName,
                    Amount = (long)(x.Amount ?? 0),
                    AmountDisplay = _helper.SetSeprator((long)(x.Amount ?? 0)) + " ریال",
                    EntryDate = x.EntryDate,
                    EntryTime = (x.EntryTime ?? "").Trim()
                });
            }

            return new AdminReportResponse<AdminGuestSessionRowDto, SalesReportSummaryDto>
            {
                Data = result,
                Summary = new SalesReportSummaryDto
                {
                    TotalCount = result.Count,
                    TotalAmount = total,
                    TotalAmountDisplay = _helper.SetSeprator(total) + " ریال"
                }
            };
        }

        // ============================================================
        //  گزارش هزینه‌ها - Acc_ArticleDoc (خواندنی)
        // ============================================================
        public async Task<AdminReportResponse<AdminExpenseRowDto, SalesReportSummaryDto>> GetExpenseReportAsync(short shiftID, string? from, string? to)
        {
            // نکته: CreationDate از نوع nchar است (ممکن است فاصله انتهایی داشته باشد) → Trim می‌شود
            var rows = await _db.Acc_ArticleDocs
                .AsNoTracking()
                .Where(d => d.ShiftID == shiftID)
                .OrderByDescending(d => d.CreationDate)
                .ThenByDescending(d => d.DocID)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(from))
                rows = rows.Where(d => d.CreationDate != null && d.CreationDate.Trim().CompareTo(from) >= 0).ToList();
            if (!string.IsNullOrWhiteSpace(to))
                rows = rows.Where(d => d.CreationDate != null && d.CreationDate.Trim().CompareTo(to) <= 0).ToList();

            var result = new List<AdminExpenseRowDto>();
            long total = 0;
            foreach (var x in rows)
            {
                total += x.Amount ?? 0;
                result.Add(new AdminExpenseRowDto
                {
                    DocID = x.DocID,
                    ArticleDesc = x.ArticleDesc,
                    ArticleID = x.ArticleID,
                    ArticleCount = (x.ArticleCount ?? "").Trim(),
                    ArticleCountUnit = (x.ArticleCountUnit ?? "").Trim(),
                    Amount = x.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(x.Amount ?? 0) + " ریال",
                    CreationDate = (x.CreationDate ?? "").Trim(),
                    CreationTime = (x.CreationTime ?? "").Trim()
                });
            }

            return new AdminReportResponse<AdminExpenseRowDto, SalesReportSummaryDto>
            {
                Data = result,
                Summary = new SalesReportSummaryDto
                {
                    TotalCount = result.Count,
                    TotalAmount = total,
                    TotalAmountDisplay = _helper.SetSeprator(total) + " ریال"
                }
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
                    t.EntryDate,
                    t.EntryDateTime,
                    t.IsGuest,
                    FullName = p != null ? p.FullName : "",
                    SportName = ms != null && ms.Gen_SportSanse != null
                                ? (ms.Gen_SportSanse.Gen_Sport_Category != null ? ms.Gen_SportSanse.Gen_Sport_Category.SportName + " " : "") + (ms.Gen_SportSanse.SanseName ?? "")
                                : ""
                })
                .ToListAsync();

            var rows = raw.Select(x =>
            {
                // ✅ زمان ورود: ترجیحاً از EntryDateTime، وگرنه از تاریخ+ساعت متنی
                DateTime? entry = x.EntryDateTime;
                if (!entry.HasValue && !string.IsNullOrWhiteSpace(x.EntryDate))
                {
                    try
                    {
                        var d = _helper.ToGregorian(x.EntryDate.Trim());
                        if (TimeSpan.TryParse((x.EntryTime ?? "").Trim(), out var t))
                            d = d.Add(t);
                        entry = d;
                    }
                    catch { }
                }

                int durationMinutes = entry.HasValue && entry.Value <= now
                    ? (int)(now - entry.Value).TotalMinutes
                    : 0;

                return new AdminInsideRowDto
                {
                    TrafficID = x.TrafficID,
                    PersonName = x.MemberID != null ? (string.IsNullOrEmpty(x.FullName) ? "-" : x.FullName)
                                                    : (string.IsNullOrEmpty(x.PersonName) ? "مهمان" : x.PersonName),
                    MemberCode = x.MemberID != null && x.MemberID > 0 ? _helper.SetSeprator(x.MemberID.Value) : null,
                    EntryTime = (x.EntryTime ?? "").Trim(),
                    SportName = string.IsNullOrEmpty(x.SportName) ? "-" : x.SportName,
                    IsGuest = x.MemberID == null || x.IsGuest == true,
                    DurationMinutes = durationMinutes
                };
            }).ToList();

            // ✅ مرتب‌سازی بر اساس «زمان حضور» — بیشترین مدت حضور در باشگاه اول
            return rows.OrderByDescending(x => x.DurationMinutes)
                       .ThenByDescending(x => x.EntryTime)
                       .ToList();
        }

        // ============================================================
        //  سری‌زمانی نمودارها (روزانه/هفتگی/ماهانه/سالانه) — خواندنی
        // ============================================================

        private static readonly string[] PersianMonthNames =
            { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

        private static readonly string[] PersianDayNames =
            { "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه" };

        private static string PersianDayName(DateTime d)
            => PersianDayNames[((int)d.DayOfWeek + 1) % 7];

        /// <summary>نقطه صفر تاریخ شمسی (۱ فروردین سال معین) به میلادی</summary>
        private static DateTime JalaliYearStart(int jYear)
        {
            var pc = new System.Globalization.PersianCalendar();
            return pc.ToDateTime(jYear, 1, 1, 0, 0, 0, 0);
        }

        /// <summary>
        /// آمار همه بخش‌ها برای صفحه نمودارها.
        /// period: day(ساعت‌های یک روز) | week(۷ روز با نام روز) | month(روزهای یک ماه) | year(۱۲ ماه شمسی)
        /// offset: ۰ = دوره جاری، منفی = عقب‌تر در گذشته (پیمایش)
        /// </summary>
        public async Task<ChartSeriesResponseDto> GetChartSeriesAsync(short shiftID, string period, int offset = 0)
        {
            var pc = new System.Globalization.PersianCalendar();
            string ToShamsi(DateTime d) => $"{pc.GetYear(d):0000}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}";
            var now = DateTime.Now;
            var today = now.Date;

            // --- محاسبه بازه‌ها بر اساس نوع دوره + پیمایش ---
            DateTime curFrom, curTo, prevFrom, prevTo;
            string rangeTitle;
            bool canGoNext;

            if (period == "day")
            {
                var day = today.AddDays(offset);
                curFrom = day; curTo = day;
                prevFrom = day.AddDays(-1); prevTo = day.AddDays(-1);
                rangeTitle = offset == 0 ? "امروز" : PersianDayName(day) + " «" + ToShamsi(day) + "»";
                canGoNext = offset < 0;
            }
            else if (period == "week")
            {
                // هفته شمسی: از شنبه تا جمعه. offset=0 → هفته جاری
                var daysFromSat = ((int)today.DayOfWeek + 1) % 7; // شنبه = 0
                var satThis = today.AddDays(-daysFromSat);
                curFrom = satThis.AddDays(offset * 7);
                curTo = curFrom.AddDays(6);
                prevFrom = curFrom.AddDays(-7); prevTo = prevFrom.AddDays(6);
                rangeTitle = "هفته " + ToShamsi(curFrom);
                canGoNext = curTo < today;
            }
            else if (period == "month")
            {
                var jy = pc.GetYear(now);
                var jm = pc.GetMonth(now) + offset;
                int jy2 = jy;
                while (jm < 1) { jm += 12; jy2--; }
                while (jm > 12) { jm -= 12; jy2++; }
                curFrom = pc.ToDateTime(jy2, jm, 1, 0, 0, 0, 0);
                var daysInMonth = pc.GetDaysInMonth(jy2, jm);
                curTo = pc.ToDateTime(jy2, jm, daysInMonth, 0, 0, 0, 0);
                var pm = jm - 1;
                var py = jy2;
                if (pm < 1) { pm = 12; py--; }
                prevFrom = pc.ToDateTime(py, pm, 1, 0, 0, 0, 0);
                prevTo = pc.ToDateTime(py, pm, pc.GetDaysInMonth(py, pm), 0, 0, 0, 0);
                rangeTitle = PersianMonthNames[jm - 1] + " " + jy2;
                canGoNext = curTo < today;
            }
            else // year
            {
                var jy = pc.GetYear(now) + offset;
                curFrom = JalaliYearStart(jy);
                curTo = JalaliYearStart(jy + 1).AddDays(-1);
                prevFrom = JalaliYearStart(jy - 1);
                prevTo = JalaliYearStart(jy).AddDays(-1);
                rangeTitle = "سال " + jy;
                canGoNext = curTo < today;
            }

            var curFromS = ToShamsi(curFrom);
            var curToS = ToShamsi(curTo);
            var prevFromS = ToShamsi(prevFrom);
            var prevToS = ToShamsi(prevTo);
            var rangeFromS = prevFromS; // کل بازه بارگذاری: از ابتدای دوره قبل تا انتهای دوره جاری
            var rangeToS = curToS;

            // --- ترددها. نکته مهم شیفت بانوان: در این دیتابیس ۳۵۲ هزار ردیف ShiftID=2 همه
            //     TrafficStatus=0 دارند (فرمت قدیمی کیوسک) و EntryDate/EntryTime آنها زمان ورود واقعی است.
            //     پس «ورود» = هر ردیف با EntryDate معتبر (صرف‌نظر از Status)، همان کاری که گزارش تردد UI انجام می‌دهد.
            var trafficRows = await _db.ACC_Traffics.AsNoTracking()
                .Where(t => t.ShiftID == shiftID && t.EntryDate != null
                         && t.EntryDate.CompareTo(rangeFromS) >= 0
                         && t.EntryDate.CompareTo(rangeToS) <= 0)
                .Select(t => new { t.EntryDate, t.EntryTime, t.TrafficStatus, t.SportMemberID })
                .ToListAsync();

            // --- ثبت‌نام/تمدید ---
            var registerRows = await _db.Acc_MemberSports.AsNoTracking()
                .Where(ms => ms.CreationDate != null
                          && ms.Gen_SportSanse != null && ms.Gen_SportSanse.ShiftID == shiftID
                          && ms.CreationDate.CompareTo(rangeFromS) >= 0
                          && ms.CreationDate.CompareTo(rangeToS) <= 0)
                .Select(ms => new
                {
                    ms.CreationDate,
                    ms.SportSanseID,
                    SanseName = ms.Gen_SportSanse.SanseName,
                    SportName = ms.Gen_SportSanse.Gen_Sport_Category.SportName
                })
                .ToListAsync();

            // --- بلیط تک‌جلسه ---
            var ticketRows = await _db.ACC_Tickets.AsNoTracking()
                .Where(t => t.ShiftID == shiftID && t.CreationDate != null
                         && t.CreationDate.CompareTo(rangeFromS) >= 0
                         && t.CreationDate.CompareTo(rangeToS) <= 0)
                .Select(t => new { t.CreationDate, SansName = t.Gen_Tarefe.Gen_San.Sans })
                .ToListAsync();

            // --- خدمات ---
            var serviceRows = await _db.ACC_MemberServices.AsNoTracking()
                .Where(s => s.ShiftID == shiftID && s.CreationDate != null
                         && s.CreationDate.CompareTo(rangeFromS) >= 0
                         && s.CreationDate.CompareTo(rangeToS) <= 0)
                .Select(s => new { s.CreationDate, ServiceName = s.Gen_Service.ServiceDesc, s.ServiceAmount })
                .ToListAsync();

            // --- دریافتی‌ها (سند اعتبار، هماهنگ با GetDashboardStatsAsync) ---
            var financeRows = await (
                from c in _db.Cash_CreditStatments.AsNoTracking()
                where c.CreationDate != null
                   && c.CreationDate.CompareTo(rangeFromS) >= 0
                   && c.CreationDate.CompareTo(rangeToS) <= 0
                join m in _db.Gen_Members on c.MemberID equals m.MemberID into mj
                from m in mj.DefaultIfEmpty()
                join t in _db.ACC_Traffics on c.TrafficID equals t.TrafficID into tj
                from t in tj.DefaultIfEmpty()
                where m.ShiftID == shiftID || t.ShiftID == shiftID
                select new { c.CreationDate, Amount = c.Amount ?? 0, c.CreditTypeID })
                .ToListAsync();

            // --- ساخت باکِت‌ها + برچسب‌ها بر اساس نوع دوره ---
            Dictionary<string, ChartSeriesPointDto> BuildBuckets(DateTime from, DateTime to)
            {
                var dict = new Dictionary<string, ChartSeriesPointDto>();

                if (period == "year")
                {
                    // ۱۲ ماه شمسی سال — همه از ابتدای ماه
                    var jy = pc.GetYear(from);
                    for (var m = 1; m <= 12; m++)
                    {
                        var key = $"{jy:0000}/{m:00}";
                        dict[key] = new ChartSeriesPointDto
                        {
                            BucketKey = key,
                            ShortLabel = PersianMonthNames[m - 1],
                            FullLabel = PersianMonthNames[m - 1] + " " + jy
                        };
                    }
                }
                else if (period == "day")
                {
                    // ۲۴ ساعت همان روز — برچسب فقط ساعت
                    for (var h = 0; h < 24; h++)
                    {
                        var key = h.ToString("00");
                        dict[key] = new ChartSeriesPointDto
                        {
                            BucketKey = key,
                            ShortLabel = h.ToString("00") + ":00",
                            FullLabel = ToShamsi(from) + " ساعت " + h.ToString("00") + ":00"
                        };
                    }
                }
                else
                {
                    // روزهای بازه (هفته/ماه) — برچسب: نام روز هفته یا روز ماه
                    for (var d = from; d <= to; d = d.AddDays(1))
                    {
                        var key = ToShamsi(d);
                        var lbl = period == "week" ? PersianDayName(d) : pc.GetDayOfMonth(d).ToString("00");
                        dict[key] = new ChartSeriesPointDto
                        {
                            BucketKey = key,
                            ShortLabel = lbl,
                            FullLabel = PersianDayName(d) + " «" + key + "»"
                        };
                    }
                }
                return dict;
            }

            var dictCur = BuildBuckets(curFrom, curTo);
            var dictPrev = BuildBuckets(prevFrom, prevTo);

            void Add(DateTime shamsiDate, int hour, Action<ChartSeriesPointDto> add)
            {
                string? key;
                if (period == "year")
                {
                    var s = ToShamsi(shamsiDate);
                    key = s.Substring(0, 7);
                }
                else if (period == "day")
                {
                    key = hour.ToString("00");
                }
                else
                {
                    key = ToShamsi(shamsiDate);
                }

                if (dictCur.TryGetValue(key, out var c)) add(c);
                else if (dictPrev.TryGetValue(key, out var p)) add(p);
            }

            // --- توزیع ترددها: هر ردیف با EntryDate معتبر یک ورود است ---
            foreach (var t in trafficRows)
            {
                if (t.EntryDate == null || t.EntryDate.Length < 10) continue;
                var parts = t.EntryDate.Substring(0, 10).Split('/');
                if (parts.Length != 3) continue;
                DateTime gd;
                try
                {
                    gd = pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
                }
                catch { continue; }

                // ساعت ورود برای باکِت‌های «روزانه»
                var hour = 0;
                var timeStr = (t.EntryTime ?? "").Trim();
                if (timeStr.Length >= 2 && int.TryParse(timeStr.Substring(0, 2), out var hh))
                    hour = Math.Clamp(hh, 0, 23);

                Add(gd, hour, p => p.TrafficCount++);
            }

            foreach (var r in registerRows)
            {
                if (r.CreationDate == null || r.CreationDate.Length < 10) continue;
                var parts = r.CreationDate.Substring(0, 10).Split('/');
                if (parts.Length != 3) continue;
                try
                {
                    var gd = pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
                    Add(gd, 0, p => p.RegisterCount++);
                }
                catch { }
            }

            foreach (var t in ticketRows)
            {
                if (t.CreationDate == null || t.CreationDate.Length < 10) continue;
                var parts = t.CreationDate.Substring(0, 10).Split('/');
                if (parts.Length != 3) continue;
                try
                {
                    var gd = pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
                    Add(gd, 0, p => p.TicketCount++);
                }
                catch { }
            }

            foreach (var s in serviceRows)
            {
                if (s.CreationDate == null || s.CreationDate.Length < 10) continue;
                var parts = s.CreationDate.Substring(0, 10).Split('/');
                if (parts.Length != 3) continue;
                try
                {
                    var gd = pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
                    Add(gd, 0, p => p.ServiceCount++);
                }
                catch { }
            }

            foreach (var f in financeRows)
            {
                if (f.CreationDate == null || f.CreationDate.Length < 10) continue;
                var parts = f.CreationDate.Substring(0, 10).Split('/');
                if (parts.Length != 3) continue;
                try
                {
                    var gd = pc.ToDateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0, 0, 0, 0);
                    Add(gd, 0, p => p.FinanceAmount += f.Amount);
                }
                catch { }
            }

            // --- خلاصه هر سری (جاری / قبل) — با کلید باکِت تا هم‌ساخت باشد ---
            long SumFromDict(Dictionary<string, ChartSeriesPointDto> dict, Func<ChartSeriesPointDto, long> sel)
                => dict.Values.Sum(sel);

            // --- سهم سانس‌ها از هر بخش (نمودار دایره‌ای) ---
            // ترددها: نگاشت SportMemberID → سانس از ثبت‌نام‌ها + جدول Acc_MemberSports
            var sanseNameById = registerRows
                .Where(r => r.SportSanseID.HasValue)
                .GroupBy(r => r.SportSanseID.Value)
                .ToDictionary(g => g.Key, g =>
                    (g.First().SportName ?? "") + " - " + (g.First().SanseName ?? "").Trim());

            var trafficSanses = await _db.ACC_Traffics.AsNoTracking()
                .Where(t => t.ShiftID == shiftID && t.EntryDate != null
                         && t.EntryDate.CompareTo(rangeFromS) >= 0
                         && t.EntryDate.CompareTo(rangeToS) <= 0
                         && t.SportMemberID != null)
                .Join(_db.Acc_MemberSports,
                    t => t.SportMemberID!,
                    ms => ms.SportMemberID,
                    (t, ms) => new { ms.SportSanseID })
                .ToListAsync();

            ChartShareDto BuildShare(Dictionary<string, long> raw, string unit)
            {
                var share = new ChartShareDto();
                var grandTotal = raw.Values.Sum();
                if (grandTotal <= 0) return share;

                // برش‌های ≥ ۲٪ جدا، بقیه در «سایر سانس‌ها»
                var big = raw.Where(kv => kv.Value * 100.0 / grandTotal >= 2.0)
                             .OrderByDescending(kv => kv.Value).ToList();
                long others = 0;
                foreach (var kv in raw.Where(kv => kv.Value * 100.0 / grandTotal < 2.0)) others += kv.Value;

                foreach (var kv in big)
                {
                    var pct = Math.Round(kv.Value * 100.0 / grandTotal, 1);
                    share.Slices.Add(new ChartShareSliceDto
                    {
                        Label = kv.Key,
                        Value = kv.Value,
                        Percent = pct,
                        PercentDisplay = pct.ToString("0.#") + "٪"
                    });
                }
                if (others > 0)
                {
                    var opct = Math.Round(others * 100.0 / grandTotal, 1);
                    share.OthersValue = others;
                    share.OthersPercentDisplay = opct.ToString("0.#") + "٪";
                }
                return share;
            }

            // تردد بر اساس سانس
            var trafficBySanse = new Dictionary<string, long>();
            foreach (var t in trafficSanses)
            {
                if (t.SportSanseID == null) continue;
                var name = sanseNameById.TryGetValue(t.SportSanseID.Value, out var n) ? n : "سانس نامشخص";
                trafficBySanse[name] = trafficBySanse.GetValueOrDefault(name) + 1;
            }

            // ثبت‌نام بر اساس سانس (ثبت‌نام + تمدید = هر ردیف)
            var registerBySanse = new Dictionary<string, long>();
            foreach (var r in registerRows)
            {
                if (r.SportSanseID == null) continue;
                var name = sanseNameById.TryGetValue(r.SportSanseID.Value, out var n) ? n : "سانس نامشخص";
                registerBySanse[name] = registerBySanse.GetValueOrDefault(name) + 1;
            }

            // بلیط بر اساس سانس
            var ticketBySanse = new Dictionary<string, long>();
            foreach (var t in ticketRows)
            {
                var name = string.IsNullOrWhiteSpace(t.SansName) ? "بدون سانس" : t.SansName!.Trim();
                ticketBySanse[name] = ticketBySanse.GetValueOrDefault(name) + 1;
            }

            // خدمات بر اساس نوع خدمت
            var serviceByType = new Dictionary<string, long>();
            foreach (var s in serviceRows)
            {
                var name = string.IsNullOrWhiteSpace(s.ServiceName) ? "خدمات متفرقه" : s.ServiceName!.Trim();
                serviceByType[name] = serviceByType.GetValueOrDefault(name) + 1;
            }

            // دریافتی‌ها بر اساس نوع سند
            var financeByType = new Dictionary<string, long>();
            foreach (var f in financeRows)
            {
                var name = GetCreditTypeDesc(f.CreditTypeID);
                financeByType[name] = financeByType.GetValueOrDefault(name) + f.Amount;
            }

            var res = new ChartSeriesResponseDto
            {
                Points = dictCur.Values.OrderBy(p => p.BucketKey).ToList(),
                PointsPrev = dictPrev.Values.OrderBy(p => p.BucketKey).ToList(),
                CurrentRangeLabel = rangeTitle + " («" + curFromS + "» تا «" + curToS + "»)",
                PreviousRangeLabel = "«" + prevFromS + "» تا «" + prevToS + "»",
                CanGoNext = canGoNext,
                Traffic = new ChartSerieSummaryDto { Total = SumFromDict(dictCur, p => p.TrafficCount), PreviousTotal = SumFromDict(dictPrev, p => p.TrafficCount) },
                Register = new ChartSerieSummaryDto { Total = SumFromDict(dictCur, p => p.RegisterCount), PreviousTotal = SumFromDict(dictPrev, p => p.RegisterCount) },
                Ticket = new ChartSerieSummaryDto { Total = SumFromDict(dictCur, p => p.TicketCount), PreviousTotal = SumFromDict(dictPrev, p => p.TicketCount) },
                Service = new ChartSerieSummaryDto { Total = SumFromDict(dictCur, p => p.ServiceCount), PreviousTotal = SumFromDict(dictPrev, p => p.ServiceCount) },
                Finance = new ChartSerieSummaryDto { Total = SumFromDict(dictCur, p => p.FinanceAmount), PreviousTotal = SumFromDict(dictPrev, p => p.FinanceAmount) },
                TrafficShare = BuildShare(trafficBySanse, "ورود"),
                RegisterShare = BuildShare(registerBySanse, "نفر"),
                TicketShare = BuildShare(ticketBySanse, "بلیط"),
                ServiceShare = BuildShare(serviceByType, "سرویس"),
                FinanceShare = BuildShare(financeByType, "ریال")
            };
            res.Finance.TotalDisplay = _helper.SetSeprator(res.Finance.Total) + " ریال";
            return res;
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