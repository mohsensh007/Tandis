using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.Models;

namespace TandisWebApp.Controllers;

[MemberAuthorize]
public class HomeController : Controller
{
    /// <summary>داشبورد اصلی</summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // ✅ موقتاً از برنامه حذف شد (درخواست کاربر — بعداً دوباره اضافه می‌شود):
    //    ورود/خرجو از باشگاه توسط دستگاه تردد باشگاه ثبت می‌شود، نه از داخل اپلیکیشن.
    //    کافی است لینک «ورود / خروج» از منوی داشبورد برداشته شود تا صفحه دسترس‌پذیر نباشد.
    //    برای برگرداندن، این متد را از حالت کامنت خارج کنید:
    //
    // [HttpGet]
    // public IActionResult ScanQr()
    // {
    //     ViewData["Title"] = "اسکن QR Code ورود";
    //     return View();
    // }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    /// <summary>صفحه گزارش‌ها (مالی + تردد)</summary>
    [HttpGet]
    public IActionResult Reports()
    {
        return View();
    }
}