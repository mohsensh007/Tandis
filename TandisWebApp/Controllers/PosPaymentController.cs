using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    /// <summary>
    /// وضعیت تراکنش پرداخت POS وب‌اپ
    /// کلاینت بعد از ارسال مبلغ به دستگاه، این endpoint را poll می‌کند
    /// تا کارت کشیده شد و نتیجه مشخص شود، سپس خرید را Confirm می‌کند.
    /// </summary>
    [MemberAuthorize]
    public class PosPaymentController : Controller
    {
        private readonly PosPaymentService _pos;

        public PosPaymentController(PosPaymentService pos)
        {
            _pos = pos;
        }

        [HttpGet]
        [Route("api/PosPayment/Status")]
        public async Task<IActionResult> Status(long txnId)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var dto = await _pos.GetStatusAsync(txnId, memberID);
            return Ok(new { success = dto.Status != "notfound", data = dto });
        }

        /// <summary>انصراف از پرداخت در انتظار (جلوگیری از ارسال به دستگاه)</summary>
        [HttpPost]
        [Route("api/PosPayment/Cancel")]
        public async Task<IActionResult> Cancel(long txnId)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            await _pos.CancelAsync(txnId, memberID);
            return Ok(new { success = true });
        }
    }
}
