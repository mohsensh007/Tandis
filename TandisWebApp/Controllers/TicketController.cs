using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [MemberAuthorize]
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
            var shiftID = User.GetShiftID();
            var list = await _ticket.GetTarefeListAsync(shiftID);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Ticket/Buy")]
        public async Task<IActionResult> Buy([FromBody] TicketBuyRequest req)
        {
            var memberID = User.GetMemberID();
            var shiftID = User.GetShiftID();
            var result = await _ticket.BuyTicketAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
        // ========================
        // ✅ تک جلسه (Single Session)
        // ========================

        [HttpGet]
        [Route("api/Ticket/SingleSessionList")]
        public async Task<IActionResult> SingleSessionList(int days = 0)
        {
            var shiftID = User.GetShiftID();
            var list = await _ticket.GetSingleSessionSansesAsync(shiftID, days);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Ticket/BuySingleSession")]
        public async Task<IActionResult> BuySingleSession([FromBody] SingleSessionBuyRequest req)
        {
            var memberID = User.GetMemberID();
            var shiftID = User.GetShiftID();
            var result = await _ticket.BuySingleSessionAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }

}
