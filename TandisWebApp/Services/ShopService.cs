using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// فروشگاه / بوفه - خرید اجناس
    /// منطق معادل UscShop در کیوسک
    /// </summary>
    public class ShopService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly PosPaymentService _pos;
        private readonly ILogger<ShopService> _logger;
        private const short WEB_USER_ID = 1;

        private const byte CREDIT_SHOP = 2; // بستانکار فروشگاه

        public ShopService(FullSportDbContext db, CommonHelperService helper, PosPaymentService pos,
                           ILogger<ShopService> logger)
        {
            _db = db;
            _helper = helper;
            _pos = pos;
            _logger = logger;
        }

        /// <summary>دریافت دسته‌بندی اجناس (بوفه یا فروشگاه)</summary>
        public async Task<List<StuffCategoryDto>> GetCategoriesAsync(bool isBuffet)
        {
            var q = from c in _db.Gen_Stuff_Categories
                    where _db.Gen_Stuffs.Any(s => s.StuffCategoryID == c.StuffCategoryID && s.IsBuffet == isBuffet)
                    orderby c.CategoryDesc
                    select new StuffCategoryDto
                    {
                        StuffCategoryID = c.StuffCategoryID,
                        CategoryDesc = c.CategoryDesc,
                        PicAddress = c.PicAddress
                    };
            return await q.ToListAsync();
        }

        /// <summary>دریافت لیست اجناس یک دسته (یا همه)</summary>
        public async Task<List<StuffDto>> GetStuffsAsync(bool isBuffet, short shiftID, int? categoryID)
        {
            var q = from s in _db.Gen_Stuffs
                    where s.IsBuffet == isBuffet
                       && s.ShiftID == shiftID
                       && (categoryID == null || categoryID <= 0 || s.StuffCategoryID == categoryID)
                    orderby s.StuffDesc
                    select new StuffDto
                    {
                        StuffID = s.StuffID,
                        StuffDesc = s.StuffDesc ?? "",
                        StuffAmount = (long)(s.StuffAmount ?? 0),
                        PicAddress = s.PicAddress,
                        StuffCategoryID = s.StuffCategoryID
                    };
            var list = await q.ToListAsync();
            foreach (var s in list)
                s.AmountDisplay = _helper.SetSeprator(s.StuffAmount) + " ریال";
            return list;
        }

        /// <summary>
        /// ثبت خرید از فروشگاه/بوفه
        /// منطق معادل InsertFactor در UscShop
        /// از اعتبار فروشگاه کسر می‌شود.
        /// </summary>
        public async Task<SimpleResponse> BuyAsync(int memberID, ShopBuyRequest req, short shiftID)
        {
            // credit = پرداخت از اعتبار | pos + بدون PosTxnId = ساخت تراکنش (Prepare)
            // pos + با PosTxnId = نهایی کردن خرید بعد از پرداخت موفق (Confirm)
            string mode = (req.Method ?? "credit").ToLowerInvariant();
            bool posPrepare = mode == "pos" && req.PosTxnId == null;
            bool posConfirm = mode == "pos" && req.PosTxnId != null;
            long posTxnId = req.PosTxnId ?? 0;
            long balanceBefore = 0;

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (req.Items == null || !req.Items.Any())
                    return new SimpleResponse { Success = false, Message = "سبد خرید خالی است" };

                // ✅ اعتبارسنجی تعداد (مثبت و کمتر از ۱۰۰)
                if (req.Items.Any(i => i.Count <= 0 || i.Count > 100))
                    return new SimpleResponse { Success = false, Message = "تعداد کالا نامعتبر است" };

                // ✅ ۱) همه کالاها رو یک‌جا از دیتابیس بخون (نه N+1)
                var requestedStuffIds = req.Items.Select(i => i.StuffID).Distinct().ToList();
                var stuffs = await _db.Gen_Stuffs
                    .AsNoTracking()
                    .Where(s => requestedStuffIds.Contains(s.StuffID))
                    .ToListAsync();

                var stuffMap = stuffs.ToDictionary(s => s.StuffID);

                // ✅ ۲) چک اینکه همه کالاها وجود دارن
                if (stuffMap.Count != requestedStuffIds.Count)
                    return new SimpleResponse { Success = false, Message = "برخی کالاها یافت نشدند" };

                // ✅ ۳) محاسبه مبلغ کل از دیتابیس (نه از کلاینت)
                long total = 0;
                var validatedItems = new List<(Gen_Stuff Stuff, int Count)>();

                foreach (var item in req.Items)
                {
                    var stuff = stuffMap[item.StuffID];

                    // چک: قیمت کالا نباید null یا صفر باشه
                    if (stuff.StuffAmount == null || stuff.StuffAmount <= 0)
                        return new SimpleResponse { Success = false, Message = $"قیمت کالا «{stuff.StuffDesc}» نامعتبر است" };

                    // ✅ چک تطابق نوع فروشگاه/بوفه
                    if (stuff.IsBuffet != req.IsBuffet)
                    {
                        var type = req.IsBuffet ? "بوفه" : "فروشگاه";
                        return new SimpleResponse { Success = false, Message = $"کالا «{stuff.StuffDesc}» متعلق به {type} نیست" };
                    }

                    long lineTotal = stuff.StuffAmount.Value * item.Count;
                    total += lineTotal;
                    validatedItems.Add((stuff, item.Count));
                }

                if (total <= 0)
                    return new SimpleResponse { Success = false, Message = "مبلغ خرید نامعتبر است" };

                // ✅ ۴) روش پرداخت
                if (mode == "credit")
                {
                    // چک اعتبار عضو (فقط برای پرداخت از اعتبار)
                    long shopCredit = await _helper.GetBuffetCreditAmountAsync(memberID);
                    if (shopCredit < total)
                    {
                        return new SimpleResponse
                        {
                            Success = false,
                            Message = $"اعتبار کافی نیست. اعتبار فعلی: {_helper.SetSeprator(shopCredit)} ریال"
                        };
                    }
                }
                else
                {
                    // پرداخت با POS
                    balanceBefore = await _helper.GetBuffetCreditAmountAsync(memberID);

                    if (posPrepare)
                    {
                        var intent = await _pos.CreateIntentAsync(memberID, total, PosPaymentService.REF_SHOP);
                        if (!intent.Success)
                            return new SimpleResponse { Success = false, Message = intent.Message };

                        await tx.CommitAsync();
                        return new SimpleResponse
                        {
                            Success = true,
                            PaymentPending = true,
                            PosTxnId = intent.Data,
                            PosAmount = total,
                            PosAmountDisplay = _helper.SetSeprator(total) + " ریال",
                            Message = "مبلغ روی دستگاه POS ارسال شد"
                        };
                    }

                    // Confirm: تراکنش باید موفق، مصرف‌نشده و هم‌مبلغ باشد
                    bool claimed = await _pos.ClaimAsync(posTxnId, memberID, total, PosPaymentService.REF_SHOP);
                    if (!claimed)
                        return new SimpleResponse
                        {
                            Success = false,
                            Message = "تراکنش پرداخت معتبر نیست یا قبلاً استفاده شده است"
                        };
                }

                // ✅ ۵) ساخت فاکتور (هدر)
                var factor = new Acc_BuffetFactor
                {
                    MemberID = memberID,
                    BuffetFactorTotalAmount = total,
                    DiscountAmount = 0,
                    PersonName = "",
                    IsReceipt = false,
                    IsPosReceipt = false,
                    IsClose = true,
                    ShiftID = shiftID,
                    UserID = WEB_USER_ID,
                    CreationDate = _helper.GetToday(),
                    CreationTime = _helper.GetThisTime()
                };
                _db.Acc_BuffetFactors.Add(factor);
                await _db.SaveChangesAsync(); // برای گرفتن BuffetFactorID

                // ✅ ۶) ردیف‌های فاکتور (با قیمت واقعی از دیتابیس)
                foreach (var (stuff, count) in validatedItems)
                {
                    _db.Acc_BuffetFactorDetails.Add(new Acc_BuffetFactorDetail
                    {
                        BuffetFactorID = factor.BuffetFactorID,
                        StuffID = stuff.StuffID,
                        Amount = stuff.StuffAmount!.Value,  // ✅ قیمت از دیتابیس
                        SCount = count,
                        CreationDate = _helper.GetToday(),
                        CreationTime = _helper.GetThisTime()
                    });
                }

                if (mode == "credit")
                {
                    // ✅ ۷) ثبت بدهی فروشگاه (فقط برای پرداخت از اعتبار)
                    _db.Cash_DebitStatements.Add(new Cash_DebitStatement
                    {
                        MemberID = memberID,
                        DebitTypeID = CREDIT_SHOP,
                        RefID = factor.BuffetFactorID,
                        Amount = total,
                        DebitDesc = req.IsBuffet ? "بابت خرید از بوفه (وب‌اپ)" : "بابت خرید از فروشگاه (وب‌اپ)",
                        UserID = WEB_USER_ID,
                        CreationTime = DateTime.Now
                    });
                }
                else
                {
                    // ✅ پرداخت POS: فاکتور خورده شده (از اعتبار کسر می‌شود)؛
                    // معادل آن به‌صورت سند بستانکار «پرداخت POS» برمی‌گردد → مانده عضو بدون تغییر
                    await _pos.NeutralizeAsync(memberID, CREDIT_SHOP, balanceBefore, total,
                        (req.IsBuffet ? "پرداخت با POS بابت خرید از بوفه (وب‌اپ)" : "پرداخت با POS بابت خرید از فروشگاه (وب‌اپ)"));
                }

                await _db.SaveChangesAsync();

                if (posConfirm)
                    await _pos.SettleRefAsync(posTxnId, factor.BuffetFactorID);

                await tx.CommitAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = $"خرید با موفقیت ثبت شد. مبلغ: {_helper.SetSeprator(total)} ریال"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در ثبت خرید برای عضو {MemberID}", memberID);
                return new SimpleResponse { Success = false, Message = "خطا در ثبت اطلاعات" };
            }
        }
    }
}
