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

    /// <summary>صفحه اسکن QR Code ورود</summary>
    [HttpGet]
    public IActionResult ScanQr()
    {
        ViewData["Title"] = "اسکن QR Code ورود";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}