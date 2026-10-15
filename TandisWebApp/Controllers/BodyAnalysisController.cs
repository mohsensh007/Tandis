using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    /// <summary>
    /// گوشه «تحلیل بدن» پنل عضو:
    ///  ۱) نمایش اطلاعات فردی (نام/سن/جنسیت از دیتابیس) + پروفایل (قد، فعالیت، سابقه، هدف، جلسات)
    ///  ۲) ثبت اندازه‌گیری‌ها (وزن، دور کمر/شکم/سینه/بازو/ران/باسن) در طول زمان
    ///  ۳) محاسبه BMI، BMR، TDEE، پروتئین، کالری هدف، وزن هدف، روند وزن، نسبت کمر به قد
    ///  ۴) نمودار تغییرات برای دیدن پیشرفت
    /// </summary>
    [MemberAuthorize]
    public class BodyAnalysisController : Controller
    {
        private readonly BodyAnalysisService _body;

        public BodyAnalysisController(BodyAnalysisService body)
        {
            _body = body;
        }

        private int MemberID => int.Parse(User.FindFirstValue("MemberID") ?? "0");

        // ============================================================
        //  صفحه تحلیل بدن
        // ============================================================
        [HttpGet("/BodyAnalysis")]
        public async Task<IActionResult> Index()
        {
            var page = await _body.GetPageAsync(MemberID);
            if (page == null) return Redirect("/Home");
            return View(page);
        }

        // ============================================================
        //  API — داده کامل صفحه (برای رفرش بدون لود مجدد)
        // ============================================================
        [HttpGet("/api/BodyAnalysis/Data")]
        public async Task<IActionResult> Data()
        {
            var page = await _body.GetPageAsync(MemberID);
            if (page == null)
                return Json(new { success = false, message = "عضو یافت نشد." });
            return Json(new { success = true, data = page });
        }

        // ============================================================
        //  API — ذخیره پروفایل (قد و غیره)
        // ============================================================
        [HttpPost("/api/BodyAnalysis/SaveProfile")]
        public async Task<IActionResult> SaveProfile([FromBody] BodyProfileDto dto)
        {
            if (dto == null) return Json(new { success = false, message = "درخواست نامعتبر است." });
            var err = await _body.SaveProfileAsync(MemberID, dto);
            if (err.Length > 0) return Json(new { success = false, message = err });

            var page = await _body.GetPageAsync(MemberID);
            return Json(new { success = true, message = "اطلاعات پروفایل ذخیره شد.", data = page });
        }

        // ============================================================
        //  API — ثبت اندازه‌گیری جدید (افزودن به تاریخچه)
        // ============================================================
        [HttpPost("/api/BodyAnalysis/SaveRecord")]
        public async Task<IActionResult> SaveRecord([FromBody] BodyRecordSaveRequest req)
        {
            if (req == null) return Json(new { success = false, message = "درخواست نامعتبر است." });

            var err = await _body.SaveRecordAsync(MemberID, req);
            if (err.Length > 0) return Json(new { success = false, message = err });

            // دریافت مجدد کل صفحه (شامل لیست آپدیت شده‌ی تاریخچه) برای ارسال به فرانت
            var page = await _body.GetPageAsync(MemberID);
            return Json(new { success = true, message = "اندازه‌گیری با موفقیت ثبت شد.", data = page });
        }

        // ============================================================
        //  API — حذف یک اندازه‌گیری
        // ============================================================
        [HttpPost("/api/BodyAnalysis/DeleteRecord")]
        public async Task<IActionResult> DeleteRecord([FromBody] BodyRecordDeleteRequest req)
        {
            if (req == null || req.RecordID <= 0)
                return Json(new { success = false, message = "درخواست نامعتبر است." });
            var err = await _body.DeleteRecordAsync(MemberID, req.RecordID);
            if (err.Length > 0) return Json(new { success = false, message = err });

            var page = await _body.GetPageAsync(MemberID);
            return Json(new { success = true, message = "رکورد حذف شد.", data = page });
        }
    }

    /// <summary>درخواست حذف اندازه‌گیری</summary>
    public class BodyRecordDeleteRequest
    {
        public int RecordID { get; set; }
    }
}
