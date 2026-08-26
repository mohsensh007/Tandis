using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [MemberAuthorize]
    public class ServiceController : Controller
    {
        private readonly ServicePurchaseService _service;

        public ServiceController(ServicePurchaseService service)
        {
            _service = service;
        }

        // ---------- Views ----------
        [HttpGet]
        public IActionResult Index() => View();

        // ---------- API ----------
        [HttpGet]
        [Route("api/Service/List")]
        public async Task<IActionResult> List()
        {
            var shiftID = User.GetShiftID();
            var list = await _service.GetServiceListAsync(shiftID);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Service/Buy")]
        public async Task<IActionResult> Buy([FromBody] ServiceBuyRequest req)
        {
            var memberID = User.GetMemberID();
            var shiftID = User.GetShiftID();
            var result = await _service.BuyServiceAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}
