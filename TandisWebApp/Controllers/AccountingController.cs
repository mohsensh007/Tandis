using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [Authorize]
    public class AccountingController : Controller
    {
        private readonly AccountingService _accounting;

        public AccountingController(AccountingService accounting)
        {
            _accounting = accounting;
        }

        // ---------- Views ----------
        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult AddMoney() => View();

        [HttpGet]
        public IActionResult Traffic() => View();

        // ---------- API ----------
        [HttpGet]
        [Route("api/Accounting/FinanceSummary")]
        public async Task<IActionResult> FinanceSummary()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var data = await _accounting.GetFinanceSummaryAsync(memberID);
            return Ok(new { success = true, data });
        }

        [HttpGet]
        [Route("api/Accounting/FinanceDocs")]
        public async Task<IActionResult> FinanceDocs()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var list = await _accounting.GetFinanceDocsAsync(memberID);
            return Ok(new { success = true, data = list });
        }

        [HttpGet]
        [Route("api/Accounting/TrafficReport")]
        public async Task<IActionResult> TrafficReport()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var list = await _accounting.GetTrafficReportAsync(memberID);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Accounting/AddMoney")]
        public async Task<IActionResult> AddMoneyApi([FromBody] AddMoneyRequest req)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var result = await _accounting.AddMoneyAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpGet]
        [Route("api/Accounting/CreditTypes")]
        public async Task<IActionResult> CreditTypes()
        {
            var list = await _accounting.GetCreditTypesAsync();
            return Ok(new { success = true, data = list });
        }
    }
}
