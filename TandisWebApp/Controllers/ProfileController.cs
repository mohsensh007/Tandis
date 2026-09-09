using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [MemberAuthorize]
    public class ProfileController : Controller
    {
        private readonly ProfileService _profile;

        public ProfileController(ProfileService profile)
        {
            _profile = profile;
        }

        // ---------- Views ----------
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var hasFace = false;
            if (int.TryParse(User.FindFirst("MemberID")?.Value, out var memberID) && memberID > 0)
            {
                hasFace = await _profile.HasMemberFaceAsync(memberID);
            }
            ViewData["HasFace"] = hasFace;
            return View();
        }

        [HttpGet]
        public IActionResult ChangePassword() => View();

        // ---------- API ----------
        [HttpGet]
        [Route("api/Profile/GetProfile")]
        public async Task<IActionResult> GetProfile()
        {
            var memberID = User.GetMemberID();
            var result = await _profile.GetProfileAsync(memberID);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/Profile/UpdatePhoto")]
        public async Task<IActionResult> UpdatePhoto([FromBody] PhotoUpdateRequest req)
        {
            var memberID = User.GetMemberID();
            var result = await _profile.UpdatePhotoAsync(memberID, req.PhotoBase64);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        [Route("api/Profile/UpdateInfo")]
        public async Task<IActionResult> UpdateInfo([FromBody] ProfileUpdateRequest req)
        {
            var memberID = User.GetMemberID();
            var result = await _profile.UpdateInfoAsync(memberID, req.FirstName, req.LastName, req.Mobile, req.BirthDate);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
        [Authorize]
        [HttpGet]
        [Route("Profile/MyFace")]
        public async Task<IActionResult> MyFace()
        {
            // ✅ خواندن MemberID مستقیم از کلیم توکن عضو
            if (!int.TryParse(User.FindFirst("MemberID")?.Value, out var memberID) || memberID <= 0)
                return Unauthorized();

            var bytes = await _profile.GetMemberFaceAsync(memberID);
            if (bytes == null || bytes.Length == 0)
                return NotFound();

            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(bytes, ProfileService.DetectImageType(bytes));
        }
    }
}
