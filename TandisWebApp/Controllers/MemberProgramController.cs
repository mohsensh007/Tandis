using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TandisWebApp.Attributes;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    /// <summary>برنامه تمرینی من — سمت عضو</summary>
    [MemberAuthorize]
    public class MemberProgramController : Controller
    {
        private readonly CoachProgramService _programs;

        public MemberProgramController(CoachProgramService programs)
        {
            _programs = programs;
        }

        /// <summary>لیست برنامه‌های من (کدوم مربی، چه تاریخی)</summary>
        [HttpGet("/MyProgram")]
        public async Task<IActionResult> Index()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var model = await _programs.GetMemberProgramsAsync(memberID);
            return View(model);
        }

        /// <summary>جزئیات یک برنامه + فیلتر روزها</summary>
        [HttpGet("/MyProgram/Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var model = await _programs.GetMemberProgramAsync(memberID, id);
            if (model == null)
                return NotFound();
            return View(model);
        }
        /// <summary>تعداد برنامه‌های فعال عضو فعلی (برای نمایش بج روی داشبورد)</summary>
        [HttpGet("/api/MyProgram/ActiveCount")]
        public async Task<IActionResult> ActiveCount()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var list = await _programs.GetMemberProgramsAsync(memberID);
            return Json(new { success = true, count = list.Count });
        }
    }
}