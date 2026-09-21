using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [MemberAuthorize]
    [CoachAuthorize]
    public class CoachController : Controller
    {
        private readonly CoachService _coach;
        private readonly CoachProgramService _programs;

        public CoachController(CoachService coach, CoachProgramService programs)
        {
            _coach = coach;
            _programs = programs;
        }

        private int CoachMemberID => int.Parse(User.FindFirstValue("MemberID") ?? "0");

        /// <summary>داشبورد مربی</summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _coach.GetDashboardAsync(CoachMemberID);
            ViewBag.UnreadMessages = await _coach.GetCoachUnreadCountAsync(CoachMemberID);
            return View(model);
        }

        /// <summary>لیست همه کلاس‌های مربی</summary>
        [HttpGet]
        public async Task<IActionResult> Classes()
        {
            var classes = await _coach.GetMyClassesAsync(CoachMemberID);
            return View(classes);
        }

        /// <summary>شاگردان یک کلاس</summary>
        [HttpGet]
        public async Task<IActionResult> ClassStudents(int id)
        {
            var (className, students) = await _coach.GetClassStudentsAsync(CoachMemberID, id);
            if (string.IsNullOrEmpty(className))
                return NotFound();
            ViewBag.ClassName = className;
            return View(students);
        }

        /// <summary>کارت شاگرد</summary>
        [HttpGet]
        public async Task<IActionResult> StudentDetails(long id)
        {
            var model = await _coach.GetStudentDetailsAsync(CoachMemberID, id);
            if (model == null)
                return NotFound();
            return View(model);
        }

        /// <summary>گزارش پورسانت</summary>
        [HttpGet]
        public async Task<IActionResult> Commission(string? fromDate = null, string? toDate = null)
        {
            var model = await _coach.GetCommissionReportAsync(CoachMemberID, fromDate, toDate);
            return View(model);
        }

        /// <summary>لیست پیام‌ها با شاگردان</summary>
        [HttpGet]
        public async Task<IActionResult> Messages()
        {
            var model = await _coach.GetMessageSummariesAsync(CoachMemberID);
            return View(model);
        }

        /// <summary>چت با یک شاگرد</summary>
        [HttpGet]
        public async Task<IActionResult> Chat(int studentID)
        {
            var model = await _coach.GetChatAsync(CoachMemberID, studentID);
            if (model == null)
                return NotFound();
            return View(model);
        }

        /// <summary>ارسال پیام به شاگرد</summary>
        [HttpPost]
        public async Task<IActionResult> SendMessage(int studentID, string? title, string body)
        {
            var success = await _coach.SendMessageToStudentAsync(CoachMemberID, studentID, title, body);
            if (success)
                return RedirectToAction("Chat", new { studentID });
            return BadRequest();
        }

        // ============================================================
        //  برنامه‌های تمرینی
        // ============================================================

        /// <summary>لیست برنامه‌های مربی</summary>
        [HttpGet]
        public async Task<IActionResult> Programs()
        {
            return View(await _programs.GetProgramListAsync(CoachMemberID));
        }

        /// <summary>فرم ساخت/ویرایش برنامه</summary>
        [HttpGet]
        public async Task<IActionResult> ProgramEdit(int id = 0)
        {
            var model = id > 0
                ? await _programs.GetProgramEditAsync(id, CoachMemberID)
                : new CoachProgramEditDto();

            if (id > 0 && model == null)
                return NotFound();

            ViewBag.Students = await _programs.GetStudentOptionsAsync(CoachMemberID);
            ViewBag.ItemOptions = await _programs.GetItemOptionsAsync();
            return View(model);
        }

        /// <summary>ذخیره برنامه</summary>
        [HttpPost]
        public async Task<IActionResult> SaveProgram([FromBody] SaveProgramRequest req)
        {
            var (ok, msg, prgID) = await _programs.SaveProgramAsync(CoachMemberID, req);
            return Ok(new { success = ok, message = msg, prgID });
        }

        /// <summary>حذف برنامه</summary>
        [HttpPost]
        public async Task<IActionResult> DeleteProgram([FromBody] int prgID)
        {
            var (ok, msg) = await _programs.DeleteProgramAsync(prgID, CoachMemberID);
            return Ok(new { success = ok, message = msg });
        }
        /// <summary>جزئیات برنامه (گروه‌بندی بر اساس روز)</summary>
        [HttpGet]
        public async Task<IActionResult> ProgramDetails(int id)
        {
            var model = await _programs.GetProgramDetailsAsync(id, CoachMemberID);
            if (model == null)
                return NotFound();
            return View(model);
        }
        /// <summary>همه شاگردان مربی (JSON برای مودال)</summary>
        [HttpGet]
        public async Task<IActionResult> AllStudents()
        {
            var list = await _programs.GetAllStudentsAsync(CoachMemberID);
            return Ok(new { success = true, data = list });
        }
    }
}