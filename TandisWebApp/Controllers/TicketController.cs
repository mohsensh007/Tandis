using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [Authorize]
    public class TicketController : Controller
    {
        private readonly TicketService _ticket;

        public TicketController(TicketService ticket)
        {
            _ticket = ticket;
        }

        // ---------- Views ----------
        [HttpGet]
        public IActionResult Index() => View();

        // ---------- API ----------
        [HttpGet]
        [Route("api/Ticket/TarefeList")]
        public async Task<IActionResult> TarefeList()
        {
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var list = await _ticket.GetTarefeListAsync(shiftID);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Ticket/Buy")]
        public async Task<IActionResult> Buy([FromBody] TicketBuyRequest req)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var shiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1");
            var result = await _ticket.BuyTicketAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}
