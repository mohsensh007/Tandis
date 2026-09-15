using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TandisWebApp.Attributes;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [MemberAuthorize]
    [CoachAuthorize]
    public class CoachController : Controller
    {
        private readonly CoachService _coach;

        public CoachController(CoachService coach)
        {
            _coach = coach;
        }

        /// <summary>داشبورد مربی</summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var model = await _coach.GetDashboardAsync(memberID);
            return View(model);
        }
        /// <summary>لیست همه کلاس‌های مربی</summary>
        [HttpGet]
        public async Task<IActionResult> Classes()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var classes = await _coach.GetMyClassesAsync(memberID);
            return View(classes);
        }

        /// <summary>شاگردان یک کلاس</summary>
        [HttpGet]
        public async Task<IActionResult> ClassStudents(int id)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var (className, students) = await _coach.GetClassStudentsAsync(memberID, id);

            if (string.IsNullOrEmpty(className))
                return NotFound();

            ViewBag.ClassName = className;
            return View(students);
        }
        /// <summary>کارت شاگرد</summary>
        [HttpGet]
        public async Task<IActionResult> StudentDetails(long id)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var model = await _coach.GetStudentDetailsAsync(memberID, id);

            if (model == null)
                return NotFound();

            return View(model);
        }
       
        /// <summary>گزارش پورسانت</summary>
        [HttpGet]
        public async Task<IActionResult> Commission(string? fromDate = null, string? toDate = null)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var model = await _coach.GetCommissionReportAsync(memberID, fromDate, toDate);
            return View(model);
        }
    }

}