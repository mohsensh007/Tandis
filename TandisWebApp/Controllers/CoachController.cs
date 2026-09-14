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
    }
}