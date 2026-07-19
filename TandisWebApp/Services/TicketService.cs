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
    }
}
