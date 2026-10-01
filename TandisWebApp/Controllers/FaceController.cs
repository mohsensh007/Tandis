using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FaceController : ControllerBase
    {
        private readonly FaceDeviceService _face;

        public FaceController(FaceDeviceService face)
        {
            _face = face;
        }

        /// <summary>ارسال دستور ثبت چهره به دستگاه</summary>
        [HttpPost("Send")]
        public async Task<IActionResult> SendCommand([FromQuery] int memberID)
        {
            var result = await _face.SendFaceCaptureAsync(memberID);
            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data
            });
        }

        /// <summary>بررسی وضعیت چهره عضو</summary>
        [HttpGet("Status")]
        public async Task<IActionResult> GetStatus([FromQuery] int memberID)
        {
            var result = await _face.GetFaceStatusAsync(memberID);
            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                data = result.Data
            });
        }

        /// <summary>پاک‌کردن دستور چهره (بعد از ثبت موفق)</summary>
        [HttpPost("Clear")]
        public async Task<IActionResult> ClearCommand([FromQuery] int memberID)
        {
            var result = await _face.ClearFaceCommandAsync(memberID);
            return Ok(new
            {
                success = result.Success,
                message = result.Message
            });
        }
    }
}