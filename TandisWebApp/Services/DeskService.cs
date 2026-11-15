using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>عضو پیدا‌شده در میز خدمت</summary>
    public class DeskMemberDto
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = "";
        public string NationalCode { get; set; } = "";
        public short ShiftID { get; set; }
        public long SportCredit { get; set; }
        public long ShopCredit { get; set; }
        public long ServiceCredit { get; set; }
    }

    /// <summary>
    /// میز خدمت (کاربران Sec_Users با IsAdmin=false):
    ///  ۱) افزایش اعتبار  ۲) خرید بلیت  ۳) تک جلسه  ۴) ثبت‌نام  ۵) تمدید دوره
    /// همهٔ پرداخت‌ها با همان صف POS موجود (PosPaymentService) انجام می‌شود.
    /// </summary>
    public class DeskService
    {
        private const short WEB_USER_ID = 1;
        private const short CREDIT_SPORT = CommonHelperService.CR_RIALI;   // 11
        private const short CREDIT_SHOP = CommonHelperService.CR_PAY_SHOP; // 2
        private const short CREDIT_SERVICE = CommonHelperService.CR_PAY_SERVICE; // 3

        /// <summary>مالک تراکنش‌های POS میز خدمت (منفی، تا با اعضا قاطی نشود)</summary>
        public static int OwnerId(short deskUserID) => -(500000 + deskUserID);

        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly PosPaymentService _pos;
        private readonly RegisterService _register;
        private readonly ILogger<DeskService> _logger;

        public DeskService(FullSportDbContext db, CommonHelperService helper, PosPaymentService pos,
                           RegisterService register, ILogger<DeskService> logger)
        {
            _db = db;
            _helper = helper;
            _pos = pos;
            _register = register;
            _logger = logger;
        }

        // ============================================================
        //  پیدا کردن عضو
        // ============================================================

        public async Task<(bool ok, string message, DeskMemberDto? data)> FindMemberAsync(string? nationalCode, string? password, bool requirePassword)
        {
            var nc = NormalizeDigits((nationalCode ?? "").Trim());
            if (nc.Length != 10 || !nc.All(char.IsDigit))
                return (false, "کد ملی باید دقیقاً ۱۰ رقم باشد", null);

            var row = await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where p.NationalCode == nc
                select new { m, p }
            ).FirstOrDefaultAsync();

            if (row == null)
                return (false, "عضوی با این کد ملی یافت نشد", null);

            if (requirePassword &&
                !string.Equals(row.m.KioskPass ?? "", password ?? "", StringComparison.Ordinal))
                return (false, "رمز عبور صحیح نیست", null);

            var dto = new DeskMemberDto
            {
                MemberID = row.m.MemberID,
                FullName = row.p.FullName ?? $"{row.p.FirstName} {row.p.LastName}",
                NationalCode = nc,
                ShiftID = row.m.ShiftID ?? 1,
                SportCredit = await _helper.GetSportCreditAmountAsync(row.m.MemberID),
                ShopCredit = await _helper.GetBuffetCreditAmountAsync(row.m.MemberID),
                ServiceCredit = await _helper.GetServiceCreditAmountAsync(row.m.MemberID)
            };

            return (true, "", dto);
        }

        // ============================================================
        //  ۱) افزایش اعتبار
        // ============================================================

        public async Task<SimpleResponse> ChargeCreditAsync(short deskUserID, int memberID, long amount,
                                                            short creditTypeId, long? posTxnId)
        {
            if (amount <= 0)
                return new SimpleResponse { Success = false, Message = "مبلغ را صحیح وارد کنید" };

            if (creditTypeId != CREDIT_SPORT && creditTypeId != CREDIT_SHOP && creditTypeId != CREDIT_SERVICE)
                return new SimpleResponse { Success = false, Message = "نوع اعتبار نامعتبر است" };

            var typeName = creditTypeId == CREDIT_SHOP ? "فروشگاه"
                         : creditTypeId == CREDIT_SERVICE ? "خدمات" : "ورزشی";

            // ---- مرحله ۱: ارسال مبلغ به دستگاه ----
            if (posTxnId == null)
            {
                var intent = await _pos.CreateIntentAsync(memberID, amount, PosPaymentService.REF_CHARGE);
                if (!intent.Success)
                    return new SimpleResponse { Success = false, Message = intent.Message };

                return new SimpleResponse
                {
                    Success = true,
                    PaymentPending = true,
                    PosTxnId = intent.Data,
                    PosAmount = amount,
                    PosAmountDisplay = _helper.SetSeprator(amount) + " ریال",
                    Message = "مبلغ روی دستگاه POS ارسال شد"
                };
            }

            // ---- مرحله ۲: کارت کشیده شد → ثبت اعتبار ----
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var claimed = await _pos.ClaimAsync(posTxnId.Value, memberID, amount, PosPaymentService.REF_CHARGE);
                if (!claimed)
                    return new SimpleResponse { Success = false, Message = "تراکنش پرداخت معتبر نیست یا قبلاً استفاده شده است" };

                await _pos.AddPosCreditAsync(memberID, (byte)creditTypeId, amount,
                    $"افزایش اعتبار {typeName} از میز خدمت (کارشناس {deskUserID})");

                await _pos.SettleRefAsync(posTxnId.Value, memberID);
                await tx.CommitAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = $"مبلغ {_helper.SetSeprator(amount)} ریال به اعتبار {typeName} اضافه شد"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در افزایش اعتبار میز خدمت برای عضو {MemberID}", memberID);
                return new SimpleResponse { Success = false, Message = "خطا در ثبت اعتبار" };
            }
        }

        // ============================================================
        //  ۲) خرید بلیت (بدون نیاز به شناسه عضو)
        // ============================================================

        public Task<List<TicketTarefeDto>> GetTarefeAsync(short shiftID) => GetTarefeListAsync(shiftID);

        public async Task<List<TicketTarefeDto>> GetTarefeListAsync(short shiftID)
        {
            var list = await (
                from t in _db.Gen_Tarefes.AsNoTracking()
                where t.IsActive == true && t.ShiftID == shiftID
                orderby t.SansID, t.TarefeID
                select new TicketTarefeDto
                {
                    TarefeID = t.TarefeID,
                    Sans = t.Gen_San != null ? t.Gen_San.Sans : "",
                    Tarefe = t.Tarefe ?? "",
                    Amount = (long)(t.Amount ?? 0)
                }
            ).ToListAsync();

            foreach (var t in list)
                t.AmountDisplay = _helper.SetSeprator(t.Amount) + " ریال";

            return list;
        }

        public async Task<SimpleResponse> SellTicketAsync(short deskUserID, short shiftID, short tarefeID, long? posTxnId)
        {
            var tarefe = await _db.Gen_Tarefes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TarefeID == tarefeID && t.IsActive == true && t.ShiftID == shiftID);

            if (tarefe == null)
                return new SimpleResponse { Success = false, Message = "تعرفه بلیت یافت نشد" };

            long amount = tarefe.Amount ?? 0;
            int owner = OwnerId(deskUserID);

            if (posTxnId == null)
            {
                var intent = await _pos.CreateIntentAsync(owner, amount, PosPaymentService.REF_TICKET);
                if (!intent.Success)
                    return new SimpleResponse { Success = false, Message = intent.Message };

                return new SimpleResponse
                {
                    Success = true,
                    PaymentPending = true,
                    PosTxnId = intent.Data,
                    PosAmount = amount,
                    PosAmountDisplay = _helper.SetSeprator(amount) + " ریال",
                    Message = "مبلغ روی دستگاه POS ارسال شد"
                };
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var claimed = await _pos.ClaimAsync(posTxnId.Value, owner, amount, PosPaymentService.REF_TICKET);
                if (!claimed)
                    return new SimpleResponse { Success = false, Message = "تراکنش پرداخت معتبر نیست یا قبلاً استفاده شده است" };

                int nextTicketNo = (await _db.ACC_Tickets.MaxAsync(x => (int?)x.TicketNo) ?? 0) + 1;

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
                    UserID = deskUserID,
                    FullName = "فروش بلیت (میز خدمت)",
                    IsPos = true,
                    PosID = _pos.ConfiguredPosId,
                    TicketDesc = "خرید از میز خدمت",
                    ShiftID = shiftID,
                    CreationDate = _helper.GetToday(),
                    CreationTime = DateTime.Now
                };
                _db.ACC_Tickets.Add(ticket);
                await _db.SaveChangesAsync();

                _db.ACC_Traffics.Add(new ACC_Traffic
                {
                    Amount = 0,
                    FreeSportSansID = tarefe.SportSanseID,
                    EntryDesc = "صدور بلیط از میز خدمت",
                    TicketID = ticket.TicketID,
                    IsGuest = true,
                    TrafficStatus = 1,
                    UserID = deskUserID,
                    ShiftID = shiftID,
                    EntryDate = _helper.GetToday(),
                    EntryTime = DateTime.Now.TimeOfDay.ToString().Substring(0, 8),
                    EntryDateTime = DateTime.Now,
                    IsManual = true
                });
                await _db.SaveChangesAsync();

                await _pos.SettleRefAsync(posTxnId.Value, ticket.TicketID);
                await tx.CommitAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = $"بلیت شماره {ticket.TicketNo} صادر شد — مبلغ {_helper.SetSeprator(amount)} ریال"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در فروش بلیت میز خدمت");
                return new SimpleResponse { Success = false, Message = "خطا در ثبت بلیت" };
            }
        }

        // ============================================================
        //  ۳) تک جلسه امروز
        // ============================================================

        public async Task<List<SingleSessionSanseDto>> GetTodaySessionsAsync(short shiftID)
        {
            var dayMap = new List<(string Latin, string Shamsi)>();
            var dt = DateTime.Now;
            dayMap.Add((dt.DayOfWeek.ToString(), _helper.GetToday()));
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
                    StartTime = d.StartTime.HasValue ? d.StartTime.GetValueOrDefault().ToString(@"hh\:mm") : "",
                    EndTime = d.EndTime.HasValue ? d.EndTime.GetValueOrDefault().ToString(@"hh\:mm") : "",
                    DayName = w.DayName ?? "",
                    LatinName = w.LatinName ?? ""
                }
            ).AsNoTracking().ToListAsync();

            foreach (var s in list)
            {
                s.SessionDateShamsi = _helper.GetToday();
                s.SessionAmountDisplay = _helper.SetSeprator(s.SessionAmount) + " ریال";
            }

            return list.OrderBy(x => x.StartTime).ToList();
        }

        public async Task<SimpleResponse> SellSingleSessionAsync(short deskUserID, short shiftID, int sportSanseID,
                                                                 string nationalCode, long? posTxnId)
        {
            var (ok, msg, member) = await FindMemberAsync(nationalCode, null, false);
            if (!ok || member == null)
                return new SimpleResponse { Success = false, Message = msg };

            var sanse = await _db.Gen_SportSanses.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SportSanseID == sportSanseID);

            if (sanse == null)
                return new SimpleResponse { Success = false, Message = "سانس یافت نشد" };

            if (sanse.SessionAmount == null || sanse.SessionAmount <= 0)
                return new SimpleResponse { Success = false, Message = "این سانس تک‌جلسه ندارد" };

            var targetLatin = DateTime.Now.DayOfWeek.ToString();
            var hasSession = await (
                from d in _db.Gen_SportSanseDetails
                join w in _db.Gen_DayOfWeeks on d.DayID equals w.DayID
                where d.SportSanseID == sanse.SportSanseID && d.IsActive == true && w.LatinName == targetLatin
                select d
            ).AsNoTracking().AnyAsync();

            if (!hasSession)
                return new SimpleResponse { Success = false, Message = "این سانس امروز جلسه ندارد" };

            long amount = sanse.SessionAmount.Value;
            string today = _helper.GetToday();
            int owner = OwnerId(deskUserID);

            if (posTxnId == null)
            {
                var intent = await _pos.CreateIntentAsync(owner, amount, PosPaymentService.REF_ONESESSION);
                if (!intent.Success)
                    return new SimpleResponse { Success = false, Message = intent.Message };

                return new SimpleResponse
                {
                    Success = true,
                    PaymentPending = true,
                    PosTxnId = intent.Data,
                    PosAmount = amount,
                    PosAmountDisplay = _helper.SetSeprator(amount) + " ریال",
                    Message = "مبلغ روی دستگاه POS ارسال شد"
                };
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var claimed = await _pos.ClaimAsync(posTxnId.Value, owner, amount, PosPaymentService.REF_ONESESSION);
                if (!claimed)
                    return new SimpleResponse { Success = false, Message = "تراکنش پرداخت معتبر نیست یا قبلاً استفاده شده است" };

                await _db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO Acc_MemberSports (MemberID, SportSanseID, MembershipTypeID, ContractID, SessionCount,
Amount, Tax, DiscountAmount, RegDiscountPercent, RegDiscountAmount, FinalPayment,
CoachPercent, CoachAmount, CoachPercentForRevival, CoachRevivalAmount,
PeriodID, StartDate, EndDate, IsActive, IsRevival, CommentText, UserID, CreationDate, CreationTime)
VALUES ({member.MemberID}, {sanse.SportSanseID}, {sanse.MembershipTypeID}, 1, 1,
{amount}, 0, 0, 0, 0, {amount}, {sanse.CoachMoneyPercent ?? 0}, 0, {sanse.CoachPercentForRevival ?? 0}, 0,
1, {today}, {today}, 1, 0, {"تک جلسه از میز خدمت"}, {deskUserID}, {_helper.GetToday()}, {_helper.GetThisTime()})");

                var insertedId = await _db.Acc_MemberSports.AsNoTracking()
                    .Where(x => x.MemberID == member.MemberID && x.StartDate == today && x.CommentText == "تک جلسه از میز خدمت")
                    .OrderByDescending(x => x.SportMemberID)
                    .Select(x => x.SportMemberID)
                    .FirstOrDefaultAsync();

                // خنثی‌سازی بدهیِ Trigger با سند بستانکار POS (مثل کیوسک)
                await _pos.AddPosCreditAsync(member.MemberID, (byte)CommonHelperService.CR_PAY_SHAHRIE, amount,
                    "پرداخت POS بابت تک جلسه (میز خدمت)");

                await _pos.SettleRefAsync(posTxnId.Value, insertedId);
                await tx.CommitAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = $"تک جلسه امروز برای {member.FullName} ثبت شد — {_helper.SetSeprator(amount)} ریال"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در فروش تک جلسه میز خدمت");
                return new SimpleResponse { Success = false, Message = "خطا در ثبت تک جلسه" };
            }
        }

        // ============================================================
        //  ۵) تمدید دوره
        // ============================================================

        public Task<List<SportSanseDto>> GetPeriodsAsync(int memberID, short shiftID)
            => _register.GetSansesForRenewAsync(memberID, shiftID, null);

        public Task<ApiResponse<RegisterResponse>> RenewAsync(int memberID, int sportSanseID, string method,
                                                              long? posTxnId, short shiftID)
        {
            var req = new RegisterRequest
            {
                SportSanseID = sportSanseID,
                Method = method,
                PosTxnId = posTxnId
            };
            return _register.RenewRegisterAsync(memberID, req, shiftID);
        }

        // ============================================================
        //  کمکی
        // ============================================================

        private static string NormalizeDigits(string value)
        {
            return value.Replace('۰', '0').Replace('۱', '1').Replace('۲', '2')
                .Replace('۳', '3').Replace('۴', '4').Replace('۵', '5')
                .Replace('۶', '6').Replace('۷', '7').Replace('۸', '8')
                .Replace('۹', '9').Replace('٠', '0').Replace('١', '1')
                .Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
                .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7')
                .Replace('٨', '8').Replace('٩', '9');
        }
    }
}
