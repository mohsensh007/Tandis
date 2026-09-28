using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// خرید بلیت (سانس تک‌بار)
    /// منطق معادل UscTicket در کیوسک
    /// </summary>
    public class TicketService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<TicketService> _logger;
        private const short WEB_USER_ID = 1;

        // انواع بدهی
        private const byte DEBIT_RIALI = 11;

        public TicketService(FullSportDbContext db, CommonHelperService helper, ILogger<TicketService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        /// <summary>لیست تعرفه‌های بلیط</summary>
        public async Task<List<TicketTarefeDto>> GetTarefeListAsync(short shiftID)
        {
            var list = await (
                from t in _db.Gen_Tarefes
                where t.IsActive == true && t.ShiftID == shiftID
                orderby t.Gen_San.SansID, t.TarefeID
                select new TicketTarefeDto
                {
                    TarefeID = t.TarefeID,
                    Sans = t.Gen_San.Sans,
                    Tarefe = t.Tarefe ?? "",
                    Amount = (long)(t.Amount ?? 0)
                }
            ).ToListAsync();

            foreach (var t in list)
                t.AmountDisplay = _helper.SetSeprator(t.Amount) + " ریال";

            return list;
        }

        /// <summary>
        /// صدور بلیت
        /// منطق معادل InsertTicket در UscTicket
        /// مبلغ از اعتبار ورزشی عضو کسر می‌شود (چون درگاه آنلاین نداریم)
        /// </summary>
        public async Task<SimpleResponse> BuyTicketAsync(int memberID, TicketBuyRequest req, short shiftID)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var tarefe = await _db.Gen_Tarefes.FirstOrDefaultAsync(t => t.TarefeID == req.TarefeID);
                if (tarefe == null)
                    return new SimpleResponse { Success = false, Message = "تعرفه یافت نشد" };

                long amount = tarefe.Amount ?? 0;

                // شماره بلیت بعدی
                int nextTicketNo = (await _db.ACC_Tickets.MaxAsync(x => (int?)x.TicketNo) ?? 0) + 1;

                var member = await (
                    from m in _db.Gen_Members
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where m.MemberID == memberID
                    select new { m, p }
                ).FirstOrDefaultAsync();

                if (member == null)
                    return new SimpleResponse { Success = false, Message = "عضو یافت نشد" };

                // بررسی اعتبار کافی (اگر مبلغ > 0)
                if (amount > 0)
                {
                    long sportCredit = await _helper.GetSportCreditAmountAsync(memberID);
                    if (sportCredit < amount)
                    {
                        return new SimpleResponse
                        {
                            Success = false,
                            Message = $"اعتبار کافی نیست. اعتبار فعلی: {_helper.SetSeprator(sportCredit)} ریال"
                        };
                    }
                }

                // ساخت بلیت
                var ticket = new ACC_Ticket
                {
                    TicketNo = nextTicketNo,
                    SansID = tarefe.SansID,
                    TarefeID = tarefe.TarefeID,
                    PersonCount = 1,
                    KidCount = 0,
                    Amount = amount,
                    CardTypeID = 1,
                    CardCount = 0,
                    UserID = WEB_USER_ID,
                    FullName = member.p.FullName,
                    IsPos = false,
                    PosID = 0,
                    TicketDesc = "خرید از طریق وب‌اپ",
                    ShiftID = shiftID,
                    CreationDate = _helper.GetToday(),
                    CreationTime = DateTime.Now
                };
                _db.ACC_Tickets.Add(ticket);
                await _db.SaveChangesAsync();

                // ثبت بدهی
                if (amount > 0)
                {
                    var debit = new Cash_DebitStatement
                    {
                        MemberID = memberID,
                        DebitTypeID = DEBIT_RIALI,
                        RefID = ticket.TicketNo,
                        Amount = amount,
                        DebitDesc = "بابت خرید بلیت از وب‌اپ",
                        UserID = WEB_USER_ID,
                        CreationTime = DateTime.Now
                    };
                    _db.Cash_DebitStatements.Add(debit);
                }

                // ثبت ترافیک رایگان (صدور بلیت = ورود)
                var traffic = new ACC_Traffic
                {
                    Amount = 0,
                    FreeSportSansID = tarefe.SportSanseID,
                    EntryDesc = "صدور بلیط از وب‌اپ",
                    TicketID = ticket.TicketID,
                    IsGuest = true,
                    TrafficStatus = 1,
                    UserID = WEB_USER_ID,
                    ShiftID = shiftID,
                    EntryDate = _helper.GetToday(),
                    EntryTime = DateTime.Now.TimeOfDay.ToString().Substring(0, 8),
                    EntryDateTime = DateTime.Now,
                    IsManual = true
                };
                _db.ACC_Traffics.Add(traffic);

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = $"صدور بلیت با موفقیت انجام شد. شماره بلیت: {ticket.TicketNo}"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در صدور بلیت");
                return new SimpleResponse { Success = false, Message = "خطا در ثبت اطلاعات" };
            }
        }
        // ========================
        // ✅ تک جلسه (Single Session)
        // ========================

        /// <summary>لیست سانس‌های تک‌جلسه‌ای — daysAhead=0 فقط امروز، daysAhead=7 فردا تا ۷ روز بعد</summary>
        public async Task<List<SingleSessionSanseDto>> GetSingleSessionSansesAsync(short shiftID, int daysAhead = 0)
        {
            // ✅ ساخت نگاشت روزها: (LatinName میلادی ↔ تاریخ شمسی)
            var dayMap = new List<(string Latin, string Shamsi)>();
            int from = daysAhead > 0 ? 1 : 0;
            int to = daysAhead > 0 ? daysAhead : 0;
            for (int i = from; i <= to; i++)
            {
                var dt = DateTime.Now.AddDays(i);
                dayMap.Add((dt.DayOfWeek.ToString(), _helper.AddDaysToPersian(_helper.GetToday(), i)));
            }
            var latins = dayMap.Select(x => x.Latin).ToList();

            var list = await (
                from s in _db.Gen_SportSanses
                join d in _db.Gen_SportSanseDetails on s.SportSanseID equals d.SportSanseID
                join w in _db.Gen_DayOfWeeks on d.DayID equals w.DayID
                join cat in _db.Gen_Sport_Categories on s.SportCatID equals cat.SportCatID into catJoin
                from cat in catJoin.DefaultIfEmpty()
                join coachM in _db.Gen_Members on s.CoachMemberID equals coachM.MemberID into coachJoin
                from coachM in coachJoin.DefaultIfEmpty()
                join coachP in _db.Gen_Persons on coachM.PersonID equals coachP.PersonID into cpJoin
                from coachP in cpJoin.DefaultIfEmpty()
                where s.IsActive == true
                   && s.ShowInKiosk == true
                   && s.ShiftID == shiftID
                   && s.SessionAmount != null && s.SessionAmount > 0
                   && d.IsActive == true
                   && latins.Contains(w.LatinName ?? "")
                orderby d.StartTime
                select new SingleSessionSanseDto
                {
                    SportSanseID = s.SportSanseID,
                    SportName = cat != null ? (cat.SportName ?? "") : "",
                    SanseName = s.SanseName ?? "",
                    CoachName = coachP != null ? coachP.FullName : "",
                    SessionAmount = s.SessionAmount ?? 0,
                    StartTime = d.StartTime.HasValue ? d.StartTime.Value.ToString(@"hh\:mm") : "",
                    EndTime = d.EndTime.HasValue ? d.EndTime.Value.ToString(@"hh\:mm") : "",
                    DayName = w.DayName ?? "",
                    LatinName = w.LatinName ?? ""
                }
            ).AsNoTracking().ToListAsync();

            // ✅ نگاشت تاریخ شمسی + فرمت مبلغ
            foreach (var s in list)
            {
                var match = dayMap.FirstOrDefault(x => x.Latin == s.LatinName);
                s.SessionDateShamsi = match.Shamsi;
                s.SessionAmountDisplay = _helper.SetSeprator(s.SessionAmount) + " ریال";
            }

            return list
                .OrderBy(x => x.SessionDateShamsi)
                .ThenBy(x => x.StartTime)
                .ToList();
        }

        /// <summary>خرید تک جلسه — امروز یا تا ۷ روز آینده</summary>
        public async Task<SimpleResponse> BuySingleSessionAsync(int memberID, SingleSessionBuyRequest req, short shiftID)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var sanse = await _db.Gen_SportSanses
                    .FirstOrDefaultAsync(s => s.SportSanseID == req.SportSanseID);

                if (sanse == null)
                    return new SimpleResponse { Success = false, Message = "سانس یافت نشد" };

                if (sanse.SessionAmount == null || sanse.SessionAmount <= 0)
                    return new SimpleResponse { Success = false, Message = "این سانس تک‌جلسه ندارد" };

                // ✅ تعیین تاریخ مقصد: امروز (پیش‌فرض) یا یکی از ۷ روز آینده (با اعتبارسنجی سرور)
                string targetDate = _helper.GetToday();
                string targetLatin = DateTime.Now.DayOfWeek.ToString();

                if (!string.IsNullOrWhiteSpace(req.SessionDateShamsi))
                {
                    var reqDate = req.SessionDateShamsi.Trim();
                    bool found = false;
                    for (int i = 1; i <= 7; i++)
                    {
                        if (_helper.AddDaysToPersian(_helper.GetToday(), i) == reqDate)
                        {
                            targetDate = reqDate;
                            targetLatin = DateTime.Now.AddDays(i).DayOfWeek.ToString();
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return new SimpleResponse { Success = false, Message = "تاریخ انتخاب‌شده معتبر نیست (فقط تا ۷ روز آینده)" };
                }

                // ✅ چک اینکه سانس در روز مقصد جلسه داشته باشه
                var hasSession = await (
                    from d in _db.Gen_SportSanseDetails
                    join w in _db.Gen_DayOfWeeks on d.DayID equals w.DayID
                    where d.SportSanseID == sanse.SportSanseID
                       && d.IsActive == true
                       && w.LatinName == targetLatin
                    select d
                ).AsNoTracking().AnyAsync();

                if (!hasSession)
                    return new SimpleResponse { Success = false, Message = "این سانس در تاریخ انتخاب‌شده جلسه ندارد" };

                long amount = sanse.SessionAmount.Value;

                // ✅ چک اعتبار ورزشی
                long sportCredit = await _helper.GetSportCreditAmountAsync(memberID);
                if (sportCredit < amount)
                {
                    return new SimpleResponse
                    {
                        Success = false,
                        Message = $"اعتبار کافی نیست. اعتبار فعلی: {_helper.SetSeprator(sportCredit)} ریال"
                    };
                }

                // ✅ ثبت رکورد تک‌جلسه (SessionCount=1, StartDate=EndDate=تاریخ مقصد)
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO Acc_MemberSports (MemberID, SportSanseID, MembershipTypeID, ContractID, SessionCount,
                Amount, Tax, DiscountAmount, RegDiscountPercent, RegDiscountAmount, FinalPayment,
                CoachPercent, CoachAmount, CoachPercentForRevival, CoachRevivalAmount,
                PeriodID, StartDate, EndDate, IsActive, IsRevival, CommentText, UserID, CreationDate, CreationTime)
            VALUES ({memberID}, {sanse.SportSanseID}, {sanse.MembershipTypeID}, 1, 1,
                {amount}, 0, 0, 0, 0,
                {amount}, {sanse.CoachMoneyPercent ?? 0}, 0, {sanse.CoachPercentForRevival ?? 0}, 0,
                1, {targetDate}, {targetDate}, 1, 0,
                {"تک جلسه از وب‌اپ"}, {WEB_USER_ID}, {_helper.GetToday()}, {_helper.GetThisTime()})");

                
                // ✅ ثبت بدهی (کسر از اعتبار ورزشی) — شرح شامل تاریخِ خودِ جلسه است
                _db.Cash_DebitStatements.Add(new Cash_DebitStatement
                {
                    MemberID = memberID,
                    DebitTypeID = DEBIT_RIALI,
                    RefID = 0,
                    Amount = amount,
                    DebitDesc = $"بابت تک جلسه روز {targetDate} از وب‌اپ",
                    UserID = WEB_USER_ID,
                    CreationTime = DateTime.Now
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                var dateMsg = targetDate == _helper.GetToday() ? "امروز" : targetDate;
                return new SimpleResponse
                {
                    Success = true,
                    Message = $"تک جلسه {dateMsg} با موفقیت ثبت شد. مبلغ: {_helper.SetSeprator(amount)} ریال"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در خرید تک جلسه");
                return new SimpleResponse { Success = false, Message = "خطا در ثبت اطلاعات" };
            }
        }
    }
}
