using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ProfileService _profile;

        public ProfileController(ProfileService profile)
        {
            _profile = profile;
        }

        // ---------- Views ----------
        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult ChangePassword() => View();

        // ---------- API ----------
        [HttpGet]
        [Route("api/Profile/GetProfile")]
        public async Task<IActionResult> GetProfile()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var result = await _profile.GetProfileAsync(memberID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/Profile/UpdatePhoto")]
        public async Task<IActionResult> UpdatePhoto([FromBody] PhotoUpdateRequest req)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var result = await _profile.UpdatePhotoAsync(memberID, req.PhotoBase64);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/Profile/UpdateInfo")]
        public async Task<IActionResult> UpdateInfo([FromBody] ProfileUpdateRequest req)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var result = await _profile.UpdateInfoAsync(memberID, req.FirstName, req.LastName, req.Mobile, req.BirthDate);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}
