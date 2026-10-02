using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// خرید سرویس (ماساژ، مربیگری شخصی، ...)
    /// منطق معادل UscService در کیوسک
    /// </summary>
    public class ServicePurchaseService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly PosPaymentService _pos;
        private readonly ILogger<ServicePurchaseService> _logger;
        private const short WEB_USER_ID = 1;

        private const byte CREDIT_SERVICE = 3; // بستانکار سرویس

        public ServicePurchaseService(FullSportDbContext db, CommonHelperService helper, PosPaymentService pos,
                                      ILogger<ServicePurchaseService> logger)
        {
            _db = db;
            _helper = helper;
            _pos = pos;
            _logger = logger;
        }

        /// <summary>لیست سرویس‌ها</summary>
        public async Task<List<ServiceDto>> GetServiceListAsync(short shiftID)
        {
            var list = await (
                from s in _db.Gen_Services
                where s.ShiftID == shiftID
                orderby s.ServiceDesc
                select new ServiceDto
                {
                    ServiceID = s.ServiceID,
                    ServiceDesc = s.ServiceDesc ?? "",
                    Amount = (long)(s.Amount ?? 0),
                    PicAddress = s.PicAddress
                }
            ).ToListAsync();

            foreach (var s in list)
                s.AmountDisplay = _helper.SetSeprator(s.Amount) + " ریال";

            return list;
        }

        /// <summary>
        /// خرید سرویس
        /// منطق معادل InsertService در UscService
        /// </summary>
        public async Task<SimpleResponse> BuyServiceAsync(int memberID, ServiceBuyRequest req, short shiftID)
        {
            // credit = پرداخت از اعتبار | pos بدون PosTxnId = Prepare | pos با PosTxnId = Confirm
            string mode = (req.Method ?? "credit").ToLowerInvariant();
            bool posPrepare = mode == "pos" && req.PosTxnId == null;
            bool posConfirm = mode == "pos" && req.PosTxnId != null;
            long posTxnId = req.PosTxnId ?? 0;
            long balanceBefore = 0;

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var service = await _db.Gen_Services.FirstOrDefaultAsync(s => s.ServiceID == req.ServiceID);
                if (service == null)
                    return new SimpleResponse { Success = false, Message = "سرویس یافت نشد" };

                long amount = service.Amount ?? 0;

                if (mode == "credit")
                {
                    // اعتبار سرویس کافی است؟
                    long serviceCredit = await _helper.GetServiceCreditAmountAsync(memberID);
                    if (serviceCredit < amount)
                    {
                        return new SimpleResponse
                        {
                            Success = false,
                            Message = $"اعتبار سرویس کافی نیست. اعتبار فعلی: {_helper.SetSeprator(serviceCredit)} ریال"
                        };
                    }
                }
                else
                {
                    balanceBefore = await _helper.GetServiceCreditAmountAsync(memberID);

                    if (posPrepare)
                    {
                        var intent = await _pos.CreateIntentAsync(memberID, amount, PosPaymentService.REF_SERVICE);
                        if (!intent.Success)
                            return new SimpleResponse { Success = false, Message = intent.Message };

                        await tx.CommitAsync();
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

                    bool claimed = await _pos.ClaimAsync(posTxnId, memberID, amount, PosPaymentService.REF_SERVICE);
                    if (!claimed)
                        return new SimpleResponse
                        {
                            Success = false,
                            Message = "تراکنش پرداخت معتبر نیست یا قبلاً استفاده شده است"
                        };
                }

                // یک ترافیک ثبت می‌کنیم (در کیوسک باید روی یک Traffic سوار می‌شد)
                var traffic = new ACC_Traffic
                {
                    MemberID = memberID,
                    TrafficStatus = 2, // Service
                    EntryDesc = "خرید سرویس از وب‌اپ",
                    IsGuest = false,
                    UserID = WEB_USER_ID,
                    ShiftID = shiftID,
                    EntryDate = _helper.GetToday(),
                    EntryTime = DateTime.Now.TimeOfDay.ToString().Substring(0, 8),
                    EntryDateTime = DateTime.Now,
                    IsManual = true
                };
                _db.ACC_Traffics.Add(traffic);
                await _db.SaveChangesAsync();

                // رکورد خرید سرویس
                var ms = new ACC_MemberService
                {
                    TrafficID = traffic.TrafficID,
                    ServiceID = service.ServiceID,
                    SalonID = 1,
                    UserID = WEB_USER_ID,
                    ShiftID = shiftID,
                    ServiceAmount = amount,
                    IsReceipt = false,
                    IsPosReceipt = false,
                    ServiceDesc = "خرید از وب‌اپ",
                    CreationDate = _helper.GetToday(),
                    CreationTime = _helper.GetThisTime(),
                    AcceptedDate = DateTime.Now,
                    AgentMemberID = service.RefMemberID,
                    CommissionPercent = service.Refpercent,
                    CommissionAmount = amount * (service.Refpercent ?? 0) / 100
                };
                _db.ACC_MemberServices.Add(ms);

                // ثبت بدهی
                _db.Cash_DebitStatements.Add(new Cash_DebitStatement
                {
                    MemberID = memberID,
                    DebitTypeID = CREDIT_SERVICE,
                    RefID = ms.MemberServiceID,
                    Amount = amount,
                    DebitDesc = "بابت خرید سرویس " + service.ServiceDesc + " (وب‌اپ)",
                    UserID = WEB_USER_ID,
                    CreationTime = DateTime.Now
                });

                await _db.SaveChangesAsync();

                if (mode != "credit")
                {
                    // پرداخت POS: معادل کسرِ ثبت‌شده به‌صورت سند بستانکار برمی‌گردد → مانده بدون تغییر
                    await _pos.NeutralizeAsync(memberID, CREDIT_SERVICE, balanceBefore, amount,
                        "پرداخت با POS بابت خرید سرویس " + service.ServiceDesc + " (وب‌اپ)");
                    if (posConfirm)
                        await _pos.SettleRefAsync(posTxnId, ms.MemberServiceID);
                }

                await tx.CommitAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = $"خرید سرویس با موفقیت ثبت شد. مبلغ: {_helper.SetSeprator(amount)} ریال"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در خرید سرویس");
                return new SimpleResponse { Success = false, Message = "خطا در ثبت اطلاعات" };
            }
        }
    }
}
