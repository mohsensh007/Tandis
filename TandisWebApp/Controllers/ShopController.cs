using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [MemberAuthorize]
    public class ShopController : Controller
    {
        private readonly ShopService _shop;

        public ShopController(ShopService shop)
        {
            _shop = shop;
        }

        // ---------- Views ----------
        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult Buffet() => View();

        // ---------- API ----------
        [HttpGet]
        [Route("api/Shop/Categories")]
        public async Task<IActionResult> Categories(bool isBuffet = false)
        {
            var list = await _shop.GetCategoriesAsync(isBuffet);
            return Ok(new { success = true, data = list });
        }

        [HttpGet]
        [Route("api/Shop/Stuffs")]
        public async Task<IActionResult> Stuffs(bool isBuffet = false, int? categoryID = null)
        {
            var shiftID = User.GetShiftID();
            var list = await _shop.GetStuffsAsync(isBuffet, shiftID, categoryID);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Shop/Buy")]
        public async Task<IActionResult> Buy([FromBody] ShopBuyRequest req)
        {
            var memberID = User.GetMemberID();
            var shiftID = User.GetShiftID();
            var result = await _shop.BuyAsync(memberID, req, shiftID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}
