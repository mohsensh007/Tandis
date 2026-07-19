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
        private readonly ILogger<ShopService> _logger;
        private const short WEB_USER_ID = 1;

        private const byte CREDIT_SHOP = 2; // بستانکار فروشگاه

        public ShopService(FullSportDbContext db, CommonHelperService helper, ILogger<ShopService> logger)
        {
            _db = db;
            _helper = helper;
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
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (req.Items == null || !req.Items.Any())
                    return new SimpleResponse { Success = false, Message = "سبد خرید خالی است" };

                // جمع کل
                long total = req.Items.Sum(i => i.TotalPrice);
                if (total <= 0)
                    return new SimpleResponse { Success = false, Message = "مبلغ خرید نامعتبر است" };

                // اعتبار فروشگاه
                long shopCredit = await _helper.GetBuffetCreditAmountAsync(memberID);
                if (shopCredit < total)
                {
                    return new SimpleResponse
                    {
                        Success = false,
                        Message = $"اعتبار فروشگاه کافی نیست. اعتبار فعلی: {_helper.SetSeprator(shopCredit)} ریال"
                    };
                }

                // ساخت فاکتور (هدر)
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
                await _db.SaveChangesAsync();

                // ردیف‌های فاکتور
                foreach (var item in req.Items)
                {
                    var stuff = await _db.Gen_Stuffs.FirstOrDefaultAsync(s => s.StuffID == item.StuffID);
                    if (stuff == null) continue;

                    _db.Acc_BuffetFactorDetails.Add(new Acc_BuffetFactorDetail
                    {
                        BuffetFactorID = factor.BuffetFactorID,
                        StuffID = item.StuffID,
                        Amount = item.UnitPrice,
                        SCount = item.Count,
                        CreationDate = _helper.GetToday(),
                        CreationTime = _helper.GetThisTime()
                    });
                }

                // ثبت بدهی فروشگاه
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

                await _db.SaveChangesAsync();
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
                _logger.LogError(ex, "خطا در ثبت خرید");
                return new SimpleResponse { Success = false, Message = "خطا در ثبت اطلاعات" };
            }
        }
    }
}
