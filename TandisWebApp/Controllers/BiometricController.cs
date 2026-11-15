using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    /// <summary>
    /// احراز هویت بیومتریک (اثر انگشت / چهره) برای چهار پنل:
    /// عضو، مربی، مدیر و میز خدمت.
    /// • هر کاربرِ لاگین‌شده می‌تواند دستگاه خودش را ثبت کند.
    /// • در میز خدمت، برای «ثبت» هر بار رمز عبور لازم است.
    /// • نتیجهٔ تأیید = بلیت کوتاه‌عمر که جای رمز عبور به اکشن‌های حساس داده می‌شود.
    /// </summary>
    [Authorize]
    [Route("api/Biometric")]
    public class BiometricController : Controller
    {
        private readonly BiometricService _bio;
        private readonly DeskService _desk;

        public BiometricController(BiometricService bio, DeskService desk)
        {
            _bio = bio;
            _desk = desk;
        }

        // ============================================================
        //  تشخیص کاربر جاری
        // ============================================================
        private (string type, int id, string name) CurrentUser()
        {
            if (User.FindFirstValue("DeskAccess") == "true")
            {
                var id = int.TryParse(User.FindFirstValue("UserID"), out var u) ? u : 0;
                return ("desk", id, User.FindFirstValue("AdminUsername") ?? "desk");
            }

            if (User.FindFirstValue("IsAdmin") == "true")
            {
                var id = int.TryParse(User.FindFirstValue("UserID"), out var u) ? u : 0;
                return ("admin", id, User.FindFirstValue("AdminUsername") ?? "admin");
            }

            var memberId = int.TryParse(User.FindFirstValue("MemberID"), out var m) ? m : 0;
            var isCoach = User.FindFirstValue("RoleID") == "2";
            var name = User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "member";
            return (isCoach ? "coach" : "member", memberId, name);
        }

        private string RpId => Request.Host.Host;

        private string Origin
        {
            get
            {
                var scheme = Request.Scheme;
                var host = Request.Host.Value;
                return $"{scheme}://{host}";
            }
        }

        // ============================================================
        //  وضعیت
        // ============================================================
        [HttpGet("Status")]
        public async Task<IActionResult> Status()
        {
            var (type, id, _) = CurrentUser();
            if (id <= 0) return Ok(new { success = false, message = "کاربر شناسایی نشد" });

            var count = await _bio.CountAsync(type, id);
            return Ok(new { success = true, enrolled = count > 0, count, userType = type });
        }

        // ============================================================
        //  ثبت اثر انگشت / چهره
        // ============================================================
        [HttpPost("EnrollBegin")]
        public async Task<IActionResult> EnrollBegin()
        {
            var (type, id, name) = CurrentUser();
            if (id <= 0) return Ok(new { success = false, message = "کاربر شناسایی نشد" });

            // ✅ میز خدمت: ثبت بیومتریک فقط با رمز عبور
            if (type == "desk")
                return Ok(new { success = false, message = "برای ثبت اثر انگشت در میز خدمت، رمز عبور لازم است", needPassword = true });

            var (ok, msg, data) = await _bio.EnrollBeginAsync(type, id, name, RpId, Origin);
            return Ok(new { success = ok, message = msg, data });
        }

        [HttpPost("EnrollBeginDesk")]
        public async Task<IActionResult> EnrollBeginDesk([FromBody] DeskPasswordBody? body)
        {
            var (type, id, name) = CurrentUser();
            if (id <= 0) return Ok(new { success = false, message = "کاربر شناسایی نشد" });

            if (type != "desk")
                return Ok(new { success = false, message = "این مسیر فقط برای کاربران میز خدمت است" });

            var (okPw, msgPw, _) = await _desk.VerifyDeskPasswordAsync((short)id, body?.Password);
            if (!okPw) return Ok(new { success = false, message = msgPw });

            var (ok, msg, data) = await _bio.EnrollBeginAsync(type, id, name, RpId, Origin);
            return Ok(new { success = ok, message = msg, data });
        }

        public class DeskPasswordBody
        {
            public string? Password { get; set; }
        }

        [HttpPost("EnrollComplete")]
        public async Task<IActionResult> EnrollComplete([FromBody] BioRegisterRequest req)
        {
            var (type, id, name) = CurrentUser();
            if (id <= 0) return Ok(new { success = false, message = "کاربر شناسایی نشد" });

            var (ok, msg) = await _bio.EnrollCompleteAsync(type, id, name, Origin, req);
            return Ok(new { success = ok, message = msg });
        }

        [HttpPost("Delete")]
        public async Task<IActionResult> Delete([FromBody] DeskPasswordBody? body)
        {
            var (type, id, _) = CurrentUser();
            if (id <= 0) return Ok(new { success = false, message = "کاربر شناسایی نشد" });

            if (type == "desk")
            {
                var (okPw, msgPw, _) = await _desk.VerifyDeskPasswordAsync((short)id, body?.Password);
                if (!okPw) return Ok(new { success = false, message = msgPw });
            }

            var (ok, msg) = await _bio.DeleteAllAsync(type, id);
            return Ok(new { success = ok, message = msg });
        }

        // ============================================================
        //  تأیید (به‌جای رمز عبور)
        // ============================================================
        [HttpPost("AssertBegin")]
        public async Task<IActionResult> AssertBegin([FromBody] AssertBeginBody? body)
        {
            var (type, id, _) = CurrentUser();
            if (id <= 0) return Ok(new { success = false, message = "کاربر شناسایی نشد" });

            var (ok, msg, data) = await _bio.AssertBeginAsync(type, id, RpId, Origin);
            return Ok(new { success = ok, message = msg, data, purpose = body?.Purpose });
        }

        public class AssertBeginBody
        {
            public string? Purpose { get; set; }
        }

        [HttpPost("AssertComplete")]
        public async Task<IActionResult> AssertComplete([FromBody] BioAssertRequest req)
        {
            var (type, id, _) = CurrentUser();
            if (id <= 0) return Ok(new { success = false, message = "کاربر شناسایی نشد" });

            var (ok, msg, ticket) = await _bio.AssertCompleteAsync(type, id, Origin, req);
            return Ok(new { success = ok, message = msg, ticket });
        }
    }
}
