using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// پرداخت با دستگاه POS از وب‌اپ (مکانیزم صف):
    ///  1) وب‌اپ یک تراکنش «در انتظار» در ACC_PosTransaction می‌سازد (RefType با پیشوند WEB_)
    ///  2) Agent (روی سیستم متصل به POS) تراکنش را از DB می‌خواند و مبلغ را روی دستگاه می‌فرستد
    ///  3) وب‌اپ با poll کردن وضعیت، نتیجه را می‌گیرد و خرید را Confirm می‌کند
    ///
    ///  وضعیت تراکنش:
    ///   - ResponseCode IS NULL  → در انتظار (pending)
    ///   - ResponseCode = '00'   → پرداخت موفق
    ///   - RefID IS NULL         → هنوز به خریدی وصل نشده (مصرف نشده)
    ///   - RefID = شناسه سند     → خرید نهایی شده (مصرف شده)
    /// </summary>
    public class PosPaymentService
    {
        // پیشوند RefType برای تراکنش‌های وب‌اپ (Agent فقط این‌ها را پردازش می‌کند)
        public const string REF_SHOP = "WEB_SHOP";
        public const string REF_TICKET = "WEB_TICKET";
        public const string REF_ONESESSION = "WEB_ONESESSION";
        public const string REF_SERVICE = "WEB_SERVICE";
        public const string REF_REGISTER = "WEB_REGISTER";

        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly IConfiguration _config;
        private readonly ILogger<PosPaymentService> _logger;

        public PosPaymentService(FullSportDbContext db, CommonHelperService helper,
                                 IConfiguration config, ILogger<PosPaymentService> logger)
        {
            _db = db;
            _helper = helper;
            _config = config;
            _logger = logger;
        }

        private const short WEB_USER_ID = 1;

        /// <summary>شناسه دستگاه POS ثابت که مبلغ وب‌اپ روی آن ارسال می‌شود (از appsettings)</summary>
        public byte ConfiguredPosId => (byte)(_config.GetValue<int?>("PosPayment:PosId") ?? 1);

        /// <summary>مهلت انتظار تراکنش (دقیقه) - بعد از آن Agent آن را EXPIRED می‌کند</summary>
        public int TtlMinutes => _config.GetValue<int?>("PosPayment:TtlMinutes") ?? 5;

        // ============================================================
        //  ساخت تراکنش در انتظار (Prepare)
        // ============================================================
        public async Task<ApiResponse<long>> CreateIntentAsync(int memberID, long amount, string refType)
        {
            if (amount <= 0)
                return new ApiResponse<long> { Success = false, Message = "مبلغ پرداخت نامعتبر است" };

            var posId = ConfiguredPosId;
            var posExists = await _db.Gen_PosTbls.AnyAsync(p => p.PosID == posId);
            if (!posExists)
                return new ApiResponse<long>
                {
                    Success = false,
                    Message = $"دستگاه POS با شناسه {posId} در تنظیمات تعریف نشده است (Gen_PosTbl)"
                };

            var txn = new ACC_PosTransaction
            {
                PosID = posId,
                MainAmount = amount,
                AffectiveAmount = amount,
                RefType = refType,
                MemberID = memberID,
                CreationDate = DateTime.Now
                // ResponseCode = NULL  → در انتظار Agent
            };
            _db.ACC_PosTransactions.Add(txn);
            await _db.SaveChangesAsync();

            _logger.LogInformation("تراکنش POS وب‌اپ ساخته شد: {TxnId} مبلغ {Amount} نوع {RefType} عضو {MemberID}",
                txn.PcPosTransactionID, amount, refType, memberID);

            return new ApiResponse<long>
            {
                Success = true,
                Message = "مبلغ روی دستگاه POS ارسال شد",
                Data = txn.PcPosTransactionID
            };
        }

        // ============================================================
        //  وضعیت تراکنش (برای poll کردن کلاینت)
        // ============================================================
        public async Task<PosPaymentStatusDto> GetStatusAsync(long txnId, int memberID)
        {
            var txn = await _db.ACC_PosTransactions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PcPosTransactionID == txnId);

            if (txn == null || txn.MemberID != memberID ||
                txn.RefType == null || !txn.RefType.StartsWith("WEB_"))
            {
                return new PosPaymentStatusDto { Status = "notfound", Message = "تراکنش یافت نشد" };
            }

            long amount = (long)(txn.MainAmount ?? 0);

            if (string.IsNullOrEmpty(txn.ResponseCode))
            {
                if (txn.CreationDate.HasValue &&
                    (DateTime.Now - txn.CreationDate.Value).TotalMinutes > TtlMinutes)
                {
                    return new PosPaymentStatusDto
                    {
                        Status = "expired",
                        Amount = amount,
                        Message = "مهلت پرداخت به پایان رسید. لطفاً دوباره تلاش کنید."
                    };
                }

                // ---- صف همزمان: آیا الان روی دستگاه است یا در نوبت؟ ----
                bool processing = txn.Message == "PROCESSING";
                int ahead = 0;
                if (!processing)
                {
                    ahead = await _db.ACC_PosTransactions.CountAsync(x =>
                        x.RefType != null && x.RefType.StartsWith("WEB_")
                        && x.ResponseCode == null
                        && x.PcPosTransactionID < txnId);
                }

                return new PosPaymentStatusDto
                {
                    Status = "pending",
                    Amount = amount,
                    Processing = processing,
                    QueuePosition = ahead
                };
            }

            if (txn.ResponseCode == "00")
            {
                return new PosPaymentStatusDto
                {
                    Status = "success",
                    Amount = amount,
                    Code = "00",
                    TraceNumber = txn.TraceNumber ?? "",
                    Message = "پرداخت با موفقیت انجام شد"
                };
            }

            return new PosPaymentStatusDto
            {
                Status = "failed",
                Amount = amount,
                Code = txn.ResponseCode,
                Message = MapErrorCode(txn.ResponseCode, txn.Message)
            };
        }

        // ============================================================
        //  انصراف کاربر از پرداخت در انتظار
        //  (فقط تا وقتی Agent نتیجه‌ای ثبت نکرده باشد اثر دارد)
        // ============================================================
        public async Task CancelAsync(long txnId, int memberID)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.ACC_PosTransaction
                SET ResponseCode = 'CANCELED', Message = 'انصراف کاربر از پرداخت'
                WHERE PcPosTransactionID = {txnId}
                  AND MemberID = {memberID}
                  AND RefType LIKE 'WEB[_]%'
                  AND ResponseCode IS NULL");
        }

        private static string MapErrorCode(string code, string? serverMessage)
        {
            switch (code)
            {
                case "98":
                    return "تراکنش توسط کاربر لغو شد";
                case "CANCELED":
                    return "از پرداخت انصراف داده شد";
                case "99":
                    return "پاسخی از دستگاه دریافت نشد. لطفاً دوباره تلاش کنید.";
                case "EXPIRED":
                    return "مهلت پرداخت به پایان رسید. لطفاً دوباره تلاش کنید.";
                default:
                    var msg = string.IsNullOrWhiteSpace(serverMessage) ? "" : " | " + serverMessage;
                    return "خطا در پرداخت، کد: " + code + msg;
            }
        }

        // ============================================================
        //  Claim - مالکیت تراکنش برای نهایی کردن خرید
        //  (اتمیک: فقط یک بار و فقط وقتی موفق و مصرف‌نشده باشد)
        //  باید داخل تراکنش DB فراخوانی شود تا با rollback برگردد.
        // ============================================================
        public async Task<bool> ClaimAsync(long txnId, int memberID, long amount, string refType)
        {
            var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.ACC_PosTransaction
                SET RefID = -1
                WHERE PcPosTransactionID = {txnId}
                  AND RefID IS NULL
                  AND ResponseCode = '00'
                  AND MemberID = {memberID}
                  AND MainAmount = {amount}
                  AND RefType = {refType}");
            return affected == 1;
        }

        // ============================================================
        //  ثبت شناسه سند نهایی روی تراکنش (بعد از ساخت فاکتور/بلیت/...)
        // ============================================================
        public async Task SettleRefAsync(long txnId, long docId)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE dbo.ACC_PosTransaction
                SET RefID = {docId}
                WHERE PcPosTransactionID = {txnId} AND RefID = -1");
        }

        // ============================================================
        //  خنثی‌سازی پرداخت POS:
        //  بعد از ثبت خرید (که از اعتبار عضو کسر کرده)، معادل همان کسر را
        //  به‌صورت سند بستانکار «پرداخت POS» برمی‌گردانیم تا مانده عضو
        //  بدون تغییر بماند (دقیقاً منطق کیوسک: Cash_CreditStatment با IsPcPos)
        //
        //  balanceBefore = مانده قبل از ثبت خرید (باید قبل از writes گرفته شود)
        //  اگر هیچ کسری رخ نداده باشد (delta=0) و مبلغ>0 باشد،
        //  با pairIfZero یک زوج بدهکار/بستانکار ثبت می‌شود تا رسید پرداخت در
        //  گزارش‌ها دیده شود.
        // ============================================================
        public async Task<long> NeutralizeAsync(int memberID, byte creditTypeId, long balanceBefore,
                                                 long paidAmount, string desc, bool pairIfZero = false)
        {
            long after = await GetBalanceAsync(memberID, creditTypeId);
            long delta = balanceBefore - after;

            if (delta > 0)
            {
                await AddCreditAsync(memberID, creditTypeId, delta, desc);
                return delta;
            }

            if (pairIfZero && paidAmount > 0)
            {
                _db.Cash_DebitStatements.Add(new Cash_DebitStatement
                {
                    MemberID = memberID,
                    DebitTypeID = creditTypeId,
                    Amount = paidAmount,
                    DebitDesc = desc,
                    UserID = WEB_USER_ID,
                    CreationTime = DateTime.Now
                });
                await _db.SaveChangesAsync();
                await AddCreditAsync(memberID, creditTypeId, paidAmount, desc);
                return paidAmount;
            }

            return 0;
        }

        private async Task AddCreditAsync(int memberID, byte creditTypeId, long amount, string desc)
        {
            _db.Cash_CreditStatments.Add(new Cash_CreditStatment
            {
                MemberID = memberID,
                CreditTypeID = creditTypeId,
                Amount = amount,
                IsPos = true,
                PosID = ConfiguredPosId,
                IsPcPos = true,
                IsFische = false,
                CreditDesc = desc,
                UserID = WEB_USER_ID,
                CreationDate = _helper.GetToday(),
                CreationTime = _helper.GetThisTime()
            });
            await _db.SaveChangesAsync();
        }

        private async Task<long> GetBalanceAsync(int memberID, byte creditTypeId)
        {
            // نوع سند بستانکار → حساب مربوطه
            if (creditTypeId == CommonHelperService.CR_PAY_SHOP)
                return await _helper.GetBuffetCreditAmountAsync(memberID);
            if (creditTypeId == CommonHelperService.CR_PAY_SERVICE)
                return await _helper.GetServiceCreditAmountAsync(memberID);
            return await _helper.GetSportCreditAmountAsync(memberID);
        }
    }
}
