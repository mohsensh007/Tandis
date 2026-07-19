using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [Authorize]
    public class RegisterController : Controller
    {
        private readonly RegisterService _register;

        public RegisterController(RegisterService register)
        {
            _register = register;
        }

        // ---------- Views ----------
        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult Renew() => View();

        [HttpGet]
        public IActionResult History() => View();

        // ---------- API ----------
        [HttpGet]
        [Route("api/Register/SportCategories")]
        public async Task<IActionResult> SportCategories()
        {
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var list = await _register.GetSportCategoriesAsync(shiftID);
            return Ok(new { success = true, data = list });
        }

        [HttpGet]
        [Route("api/Register/AvailableSanses")]
        public async Task<IActionResult> AvailableSanses(int? sportCatID)
        {
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var list = await _register.GetAvailableSansesAsync(shiftID, sportCatID);
            return Ok(new { success = true, data = list });
        }

        [HttpGet]
        [Route("api/Register/SansesForRenew")]
        public async Task<IActionResult> SansesForRenew(int? sportCatID)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var list = await _register.GetSansesForRenewAsync(memberID, shiftID, sportCatID);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Register/Register")]
        public async Task<IActionResult> RegisterApi([FromBody] RegisterRequest req)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var result = await _register.RegisterAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/Register/Renew")]
        public async Task<IActionResult> RenewApi([FromBody] RegisterRequest req)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var result = await _register.RenewRegisterAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet]
        [Route("api/Register/History")]
        public async Task<IActionResult> GetHistory()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var list = await _register.GetRegisterHistoryAsync(memberID);
            return Ok(new { success = true, data = list });
        }
    }
}
