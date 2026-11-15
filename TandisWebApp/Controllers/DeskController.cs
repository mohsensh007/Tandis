using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    /// <summary>
    /// میز خدمت — کاربران Sec_Users با IsAdmin = false
    /// ۵ کارت: افزایش اعتبار / خرید بلیت / تک جلسه / ثبت‌نام / تمدید دوره
    /// </summary>
    [DeskAuthorize]
    public class DeskController : Controller
    {
        private readonly DeskService _desk;
        private readonly CommonHelperService _helper;
        private readonly PosPaymentService _pos;

        public DeskController(DeskService desk, CommonHelperService helper, PosPaymentService pos)
        {
            _desk = desk;
            _helper = helper;
            _pos = pos;
        }

        private short DeskUserID => short.TryParse(User.FindFirstValue("UserID"), out var id) ? id : (short)1;
        private short ShiftID => short.TryParse(User.FindFirstValue("AdminShiftID"), out var s) ? s : (short)1;
        private string DeskName => User.FindFirstValue("AdminUsername") ?? "کاربر";

        // ============================================================
        //  View
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["DeskUser"] = DeskName;
            ViewData["ClubName"] = await _helper.GetClubNameAsync();
            return View();
        }

        [HttpGet]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("X-Admin-Token");
            Response.Cookies.Delete("X-Access-Token");
            return RedirectToAction("Login", "Account");
        }

        // ============================================================
        //  API — جستجوی عضو
        // ============================================================
        public class FindMemberReq
        {
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
            public bool RequirePassword { get; set; } = true;
        }

        [HttpPost]
        [Route("api/Desk/FindMember")]
        public async Task<IActionResult> FindMember([FromBody] FindMemberReq req)
        {
            var (ok, msg, data) = await _desk.FindMemberAsync(req.NationalCode, req.Password, req.RequirePassword);
            if (!ok)
                return Ok(new { success = false, message = msg });

            return Ok(new
            {
                success = true,
                data = new
                {
                    memberID = data!.MemberID,
                    fullName = data.FullName,
                    nationalCode = data.NationalCode,
                    sportCredit = data.SportCredit,
                    shopCredit = data.ShopCredit,
                    serviceCredit = data.ServiceCredit
                }
            });
        }

        // ============================================================
        //  API — ۱) افزایش اعتبار
        // ============================================================
        public class ChargeReq
        {
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
            public long Amount { get; set; }
            public short CreditType { get; set; } = 11;
            public long? PosTxnId { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/Charge")]
        public async Task<IActionResult> Charge([FromBody] ChargeReq req)
        {
            var (ok, msg, member) = await _desk.FindMemberAsync(req.NationalCode, req.Password, true);
            if (!ok || member == null)
                return Ok(new { success = false, message = msg });

            long? amount = req.Amount;

            // در مرحلهٔ تأیید، مبلغ معتبر همان مبلغ تراکنش POS است (نه ورودی کلاینت)
            if (req.PosTxnId != null)
            {
                var st = await _pos.GetStatusAnyAsync(req.PosTxnId.Value);
                if (st.Status != "success")
                    return Ok(new { success = false, message = "پرداخت تأیید نشد: " + st.Message });
                amount = st.Amount;
            }

            var result = await _desk.ChargeCreditAsync(DeskUserID, member.MemberID, amount ?? 0,
                                                       req.CreditType, req.PosTxnId);

            return Ok(new
            {
                result.Success,
                result.Message,
                result.PaymentPending,
                result.PosTxnId,
                result.PosAmount,
                result.PosAmountDisplay
            });
        }

        // ============================================================
        //  API — ۲) بلیت
        // ============================================================
        [HttpPost]
        [Route("api/Desk/Tarefe")]
        public async Task<IActionResult> Tarefe()
        {
            var list = await _desk.GetTarefeAsync(ShiftID);
            return Ok(new { success = true, data = list });
        }

        public class TicketReq
        {
            public short TarefeID { get; set; }
            public long? PosTxnId { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/BuyTicket")]
        public async Task<IActionResult> BuyTicket([FromBody] TicketReq req)
        {
            var result = await _desk.SellTicketAsync(DeskUserID, ShiftID, req.TarefeID, req.PosTxnId);
            return Ok(new
            {
                result.Success,
                result.Message,
                result.PaymentPending,
                result.PosTxnId,
                result.PosAmount,
                result.PosAmountDisplay
            });
        }

        // ============================================================
        //  API — ۳) تک جلسه
        // ============================================================
        [HttpPost]
        [Route("api/Desk/Sessions")]
        public async Task<IActionResult> Sessions()
        {
            var list = await _desk.GetTodaySessionsAsync(ShiftID);
            return Ok(new { success = true, data = list });
        }

        public class SingleReq
        {
            public int SportSanseID { get; set; }
            public string? NationalCode { get; set; }
            public long? PosTxnId { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/BuySingle")]
        public async Task<IActionResult> BuySingle([FromBody] SingleReq req)
        {
            var result = await _desk.SellSingleSessionAsync(DeskUserID, ShiftID, req.SportSanseID,
                                                            req.NationalCode ?? "", req.PosTxnId);
            return Ok(new
            {
                result.Success,
                result.Message,
                result.PaymentPending,
                result.PosTxnId,
                result.PosAmount,
                result.PosAmountDisplay
            });
        }

        // ============================================================
        //  API — ۵) تمدید دوره
        // ============================================================
        public class PeriodReq
        {
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/Periods")]
        public async Task<IActionResult> Periods([FromBody] PeriodReq req)
        {
            var (ok, msg, member) = await _desk.FindMemberAsync(req.NationalCode, req.Password, true);
            if (!ok || member == null)
                return Ok(new { success = false, message = msg });

            var list = await _desk.GetPeriodsAsync(member.MemberID, ShiftID);
            return Ok(new
            {
                success = true,
                data = new
                {
                    fullName = member.FullName,
                    nationalCode = member.NationalCode,
                    sportCredit = member.SportCredit,
                    periods = list
                }
            });
        }

        public class RenewReq
        {
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
            public int SportSanseID { get; set; }
            public string Method { get; set; } = "pos";
            public long? PosTxnId { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/Renew")]
        public async Task<IActionResult> Renew([FromBody] RenewReq req)
        {
            var (ok, msg, member) = await _desk.FindMemberAsync(req.NationalCode, req.Password, true);
            if (!ok || member == null)
                return Ok(new { success = false, message = msg });

            var result = await _desk.RenewAsync(member.MemberID, req.SportSanseID, req.Method, req.PosTxnId, ShiftID);
            return Ok(new
            {
                result.Success,
                result.Message,
                result.PaymentPending,
                result.PosTxnId,
                result.PosAmount,
                result.PosAmountDisplay
            });
        }

        // ============================================================
        //  API — وضعیت / انصراف تراکنش POS
        // ============================================================
        [HttpGet]
        [Route("api/Desk/Status")]
        public async Task<IActionResult> Status(long txnId)
        {
            var dto = await _pos.GetStatusAnyAsync(txnId);
            return Ok(new { success = dto.Status != "notfound", data = dto });
        }

        [HttpPost]
        [Route("api/Desk/Cancel")]
        public async Task<IActionResult> Cancel(long txnId)
        {
            await _pos.CancelAnyAsync(txnId);
            return Ok(new { success = true });
        }
    }
}
