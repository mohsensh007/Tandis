using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    /// <summary>
    /// میز خدمات — کاربران Sec_Users با IsAdmin = false
    /// ۵ کارت: افزایش اعتبار / خرید بلیت / تک جلسه / ثبت‌نام / تمدید دوره
    /// </summary>
    [DeskAuthorize]
    public class DeskController : Controller
    {
        private readonly DeskService _desk;
        private readonly CommonHelperService _helper;
        private readonly PosPaymentService _pos;
        private readonly BiometricService _bio;
        private readonly ShopService _shop;
        private readonly ServicePurchaseService _service;
        private readonly ReceiptPrintService _receipt;
        private readonly PanelSettingsService _panel;

        public DeskController(DeskService desk, CommonHelperService helper, PosPaymentService pos, BiometricService bio,
                              ShopService shop, ServicePurchaseService service, ReceiptPrintService receipt,
                              PanelSettingsService panel)
        {
            _desk = desk;
            _helper = helper;
            _pos = pos;
            _bio = bio;
            _shop = shop;
            _service = service;
            _receipt = receipt;
            _panel = panel;
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
            ViewData["DeskCards"] = _panel.Get().DeskCards;
            return View();
        }

        /// <summary>تنظیمات پنل (کارت‌های قابل نمایش و بخش‌های دارای چاپ فیش)</summary>
        [HttpGet]
        [Route("api/Desk/Panel")]
        public IActionResult Panel()
        {
            var s = _panel.Get();
            return Ok(new { success = true, data = new { cards = s.DeskCards, print = s.DeskPrint } });
        }

        /// <summary>
        /// ✅ خروج امن: بدون رمز عبور انجام نمی‌شود.
        /// این آدرس فقط کاربر را به همان پنل برمی‌گرداند (دکمهٔ خروج در UI مودال رمز باز می‌کند).
        /// </summary>
        [HttpGet]
        public IActionResult Logout() => RedirectToAction("Index");

        public class LogoutReq
        {
            public string? Password { get; set; }
            /// <summary>بلیت تأیید بیومتریک (اثر انگشت / چهره) به‌جای رمز</summary>
            public string? BiometricTicket { get; set; }
        }

        /// <summary>✅ خروج با رمز عبور (یا اثر انگشت ثبت‌شده) — تا هر کسی نتواند دکمهٔ خروج را بزند</summary>
        [HttpPost]
        [Route("api/Desk/Logout")]
        public async Task<IActionResult> LogoutApi([FromBody] LogoutReq? req)
        {
            var ok = false;
            var msg = "";

            if (!string.IsNullOrWhiteSpace(req?.BiometricTicket))
            {
                ok = _bio.ConsumeTicket(req!.BiometricTicket, "desk", DeskUserID);
                if (!ok) msg = "تأیید اثر انگشت منقضی شده است؛ دوباره تلاش کنید";
            }
            else
            {
                (ok, msg, _) = await _desk.VerifyDeskPasswordAsync(DeskUserID, req?.Password);
            }

            if (!ok)
                return Ok(new { success = false, message = msg });

            Response.Cookies.Delete("X-Admin-Token");
            Response.Cookies.Delete("X-Access-Token");
            return Ok(new { success = true, message = "خروج انجام شد", redirect = "/Account/Login" });
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
            /// <summary>برچسب اختیاری (فروشگاه / بوفه / خدمات) برای متن رسید</summary>
            public string? Label { get; set; }
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
                                                       req.CreditType, req.PosTxnId, req.Label);

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
            /// <summary>تعداد بلیت (پیش‌فرض ۱)</summary>
            public int Qty { get; set; } = 1;
            public long? PosTxnId { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/BuyTicket")]
        public async Task<IActionResult> BuyTicket([FromBody] TicketReq req)
        {
            var result = await _desk.SellTicketAsync(DeskUserID, ShiftID, req.TarefeID, req.PosTxnId, req.Qty);
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

        /// <summary>✅ تک جلسهٔ مهمان: بدون کد ملی — فقط نام و شمارهٔ موبایل</summary>
        public class GuestSingleReq
        {
            public int SportSanseID { get; set; }
            public string? GuestName { get; set; }
            public string? GuestMobile { get; set; }
            public long? PosTxnId { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/BuySingleGuest")]
        public async Task<IActionResult> BuySingleGuest([FromBody] GuestSingleReq req)
        {
            var result = await _desk.SellGuestSingleSessionAsync(DeskUserID, ShiftID, req.SportSanseID,
                                                                 req.GuestName, req.GuestMobile, req.PosTxnId);
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
            /// <summary>فیلتر رشتهٔ ورزشی (۰ یا خالی = همهٔ رشته‌ها)</summary>
            public int? SportCatID { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/Periods")]
        public async Task<IActionResult> Periods([FromBody] PeriodReq req)
        {
            var (ok, msg, member) = await _desk.FindMemberAsync(req.NationalCode, req.Password, true);
            if (!ok || member == null)
                return Ok(new { success = false, message = msg });

            var list = await _desk.GetPeriodsAsync(member.MemberID, ShiftID, req.SportCatID);
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
        //  ✅ ۶-۸) فروشگاه / بوفه / خدمات — همان UI پنل عضو برای میز خدمات
        // ============================================================
        [HttpGet]
        public Task<IActionResult> Shop(int? member) => CommercePage("shop", member);

        [HttpGet]
        public Task<IActionResult> Buffet(int? member) => CommercePage("buffet", member);

        [HttpGet]
        public Task<IActionResult> Services(int? member) => CommercePage("services", member);

        private async Task<IActionResult> CommercePage(string kind, int? member)
        {
            ViewData["DeskUser"] = DeskName;
            ViewData["ClubName"] = await _helper.GetClubNameAsync();
            ViewData["Kind"] = kind;
            ViewData["MemberCard"] = member.HasValue && member.Value > 0
                ? await _desk.GetMemberCardAsync(member.Value)
                : null;
            return View("Commerce");
        }

        public class QuickFindReq
        {
            public string? Query { get; set; }
        }

        /// <summary>جستجوی عضو با کد ملی یا موبایل (بدون رمز — فقط برای میز خدمات)</summary>
        [HttpPost]
        [Route("api/Desk/Commerce/FindMember")]
        public async Task<IActionResult> CommerceFindMember([FromBody] QuickFindReq req)
        {
            var (ok, msg, dto) = await _desk.QuickFindMemberAsync(req?.Query);
            if (!ok || dto == null)
                return Ok(new { success = false, message = msg });

            return Ok(new
            {
                success = true,
                data = new
                {
                    memberID = dto.MemberID,
                    fullName = dto.FullName,
                    nationalCode = dto.NationalCode,
                    sportCredit = dto.SportCredit,
                    shopCredit = dto.ShopCredit,
                    serviceCredit = dto.ServiceCredit
                }
            });
        }

        public class CommerceCatReq
        {
            public bool IsBuffet { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/Commerce/Categories")]
        public async Task<IActionResult> CommerceCategories([FromBody] CommerceCatReq? req)
        {
            var list = await _shop.GetCategoriesAsync(req?.IsBuffet ?? false, ShiftID);
            return Ok(new { success = true, data = list });
        }

        public class CommerceStuffReq
        {
            public bool IsBuffet { get; set; }
            public int? CategoryID { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/Commerce/Stuffs")]
        public async Task<IActionResult> CommerceStuffs([FromBody] CommerceStuffReq? req)
        {
            var list = await _shop.GetStuffsAsync(req?.IsBuffet ?? false, ShiftID, req?.CategoryID);
            return Ok(new { success = true, data = list });
        }

        [HttpPost]
        [Route("api/Desk/Commerce/ServiceList")]
        public async Task<IActionResult> CommerceServiceList()
        {
            var list = await _service.GetServiceListAsync(ShiftID);
            return Ok(new { success = true, data = list });
        }

        public class VerifyMemberReq
        {
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
        }

        /// <summary>تأیید هویت عضو با کد ملی + رمز — در لحظهٔ پرداخت از اعتبار</summary>
        [HttpPost]
        [Route("api/Desk/Commerce/VerifyMember")]
        public async Task<IActionResult> CommerceVerifyMember([FromBody] VerifyMemberReq? req)
        {
            var (ok, msg, dto) = await _desk.FindMemberAsync(req?.NationalCode, req?.Password, true);
            if (!ok || dto == null)
                return Ok(new { success = false, message = msg });

            return Ok(new
            {
                success = true,
                data = new
                {
                    memberID = dto.MemberID,
                    fullName = dto.FullName,
                    nationalCode = dto.NationalCode,
                    sportCredit = dto.SportCredit,
                    shopCredit = dto.ShopCredit,
                    serviceCredit = dto.ServiceCredit
                }
            });
        }

        public class CommerceBuyReq
        {
            public int MemberID { get; set; }
            public List<BasketItem> Items { get; set; } = new();
            public bool IsBuffet { get; set; }
            public string Method { get; set; } = "credit";
            public long? PosTxnId { get; set; }

            // ✅ خرید برای همه باز است: اگر کد ملی + رمز داده شود، خرید روی حساب همان عضو می‌رود؛
            //    در غیر این صورت با کارت‌خوان به‌عنوان مهمان ثبت می‌شود.
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
            public string? GuestName { get; set; }
        }

        /// <summary>تشخیص عضو از روی کد ملی + رمز؛ خروجی (OK, MemberID, ErrorMessage)</summary>
        private async Task<(bool ok, int memberID, string message)> ResolveMemberAsync(string? nationalCode, string? password)
        {
            if (string.IsNullOrWhiteSpace(nationalCode) && string.IsNullOrWhiteSpace(password))
                return (true, 0, "");

            var (ok, msg, dto) = await _desk.FindMemberAsync(nationalCode, password, true);
            if (!ok || dto == null)
                return (false, 0, msg);

            return (true, dto.MemberID, "");
        }

        [HttpPost]
        [Route("api/Desk/Commerce/Buy")]
        public async Task<IActionResult> CommerceBuy([FromBody] CommerceBuyReq? req)
        {
            if (req == null)
                return Ok(new { success = false, message = "درخواست نامعتبر است" });

            string method = (req.Method ?? "credit").ToLowerInvariant();
            bool hasCredentials = !string.IsNullOrWhiteSpace(req.NationalCode) || !string.IsNullOrWhiteSpace(req.Password);

            var member = await ResolveMemberAsync(req.NationalCode, req.Password);
            if (!member.ok && (method == "credit" || hasCredentials))
                return Ok(new { success = false, message = member.message });

            int memberID = member.ok ? member.memberID : 0;
            if (memberID <= 0) memberID = req.MemberID > 0 ? req.MemberID : 0;

            // اعتبار فقط برای عضو شناخته‌شده
            if (method == "credit" && memberID <= 0)
                return Ok(new { success = false, message = "برای پرداخت از اعتبار، کد ملی و رمز عضو را وارد کنید" });

            // کارت‌خوان با عضو شناخته‌شده → روی حساب عضو، در غیر این صورت → مهمان
            if (method != "credit" && memberID <= 0)
            {
                var guest = await _shop.BuyGuestAsync(req.Items, req.IsBuffet, req.GuestName, req.PosTxnId, ShiftID);
                return Ok(new
                {
                    guest.Success,
                    guest.Message,
                    guest.PaymentPending,
                    guest.PosTxnId,
                    guest.PosAmount,
                    guest.PosAmountDisplay,
                    isGuest = true
                });
            }

            var result = await _shop.BuyAsync(memberID, new ShopBuyRequest
            {
                Items = req.Items,
                IsBuffet = req.IsBuffet,
                Method = method,
                PosTxnId = req.PosTxnId
            }, ShiftID);

            return Ok(new
            {
                result.Success,
                result.Message,
                result.PaymentPending,
                result.PosTxnId,
                result.PosAmount,
                result.PosAmountDisplay,
                isGuest = false
            });
        }

        public class CommerceServiceReq
        {
            public int MemberID { get; set; }
            public short ServiceID { get; set; }
            public string Method { get; set; } = "credit";
            public long? PosTxnId { get; set; }
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
            public string? GuestName { get; set; }
        }

        [HttpPost]
        [Route("api/Desk/Commerce/BuyService")]
        public async Task<IActionResult> CommerceBuyService([FromBody] CommerceServiceReq? req)
        {
            if (req == null)
                return Ok(new { success = false, message = "درخواست نامعتبر است" });

            string method = (req.Method ?? "credit").ToLowerInvariant();
            bool hasCredentials = !string.IsNullOrWhiteSpace(req.NationalCode) || !string.IsNullOrWhiteSpace(req.Password);

            var member = await ResolveMemberAsync(req.NationalCode, req.Password);
            if (!member.ok && (method == "credit" || hasCredentials))
                return Ok(new { success = false, message = member.message });

            int memberID = member.ok ? member.memberID : 0;
            if (memberID <= 0) memberID = req.MemberID > 0 ? req.MemberID : 0;

            if (method == "credit" && memberID <= 0)
                return Ok(new { success = false, message = "برای پرداخت از اعتبار، کد ملی و رمز عضو را وارد کنید" });

            if (method != "credit" && memberID <= 0)
            {
                var guest = await _service.BuyGuestServiceAsync(req.ServiceID, req.GuestName, req.PosTxnId, ShiftID);
                return Ok(new
                {
                    guest.Success,
                    guest.Message,
                    guest.PaymentPending,
                    guest.PosTxnId,
                    guest.PosAmount,
                    guest.PosAmountDisplay,
                    isGuest = true
                });
            }

            var result = await _service.BuyServiceAsync(memberID, new ServiceBuyRequest
            {
                ServiceID = req.ServiceID,
                Method = method,
                PosTxnId = req.PosTxnId
            }, ShiftID);

            return Ok(new
            {
                result.Success,
                result.Message,
                result.PaymentPending,
                result.PosTxnId,
                result.PosAmount,
                result.PosAmountDisplay,
                isGuest = false
            });
        }

        // ============================================================
        //  API — رشته‌ها و سانس‌ها (انتخاب رشته در ثبت‌نام/تمدید)
        // ============================================================
        [HttpPost]
        [Route("api/Desk/SportCategories")]
        public async Task<IActionResult> SportCategories()
        {
            var list = await _desk.GetSportCategoriesAsync(ShiftID);
            return Ok(new { success = true, data = list });
        }

        public class RegisterCourseReq
        {
            public int MemberID { get; set; }
            public int SportSanseID { get; set; }
            public string Method { get; set; } = "credit";
            public long? PosTxnId { get; set; }
            public string? NationalCode { get; set; }
            public string? Password { get; set; }
        }

        /// <summary>ثبت‌نام/تمدید دوره از میز خدمات — روی همان منطق تمدید عضو (اعتبار یا کارت‌خوان)</summary>
        [HttpPost]
        [Route("api/Desk/RegisterCourse")]
        public async Task<IActionResult> RegisterCourse([FromBody] RegisterCourseReq req)
        {
            if (req == null || req.SportSanseID <= 0)
                return Ok(new { success = false, message = "سانس را انتخاب کنید" });

            int memberID = req.MemberID;

            // اگر کد ملی و رمز داده شده، عضو از همان‌جا تشخیص داده می‌شود
            if (memberID <= 0 && !string.IsNullOrWhiteSpace(req.NationalCode))
            {
                var (ok, msg, dto) = await _desk.FindMemberAsync(req.NationalCode, req.Password, true);
                if (!ok || dto == null)
                    return Ok(new { success = false, message = msg });
                memberID = dto.MemberID;
            }

            if (memberID <= 0)
                return Ok(new { success = false, message = "عضو مشخص نشده است" });

            var result = await _desk.RegisterCourseAsync(memberID, req.SportSanseID, req.Method, req.PosTxnId, ShiftID);
            return Ok(new
            {
                result.Success,
                result.Message,
                result.PaymentPending,
                result.PosTxnId,
                result.PosAmount,
                result.PosAmountDisplay,
                register = result.Data == null ? null : new
                {
                    result.Data.SportMemberID,
                    result.Data.StartDate,
                    result.Data.EndDate,
                    result.Data.FinalPayment,
                    result.Data.FinalPaymentDisplay
                }
            });
        }

        // ============================================================
        //  API — چاپ فیش روی پرینتر حرارتی POS
        // ============================================================
        /// <summary>نام پرینتر POS نصب‌شده روی سیستم سرور (برای نمایش در UI)</summary>
        [HttpGet]
        [Route("api/Desk/Printer")]
        public IActionResult Printer()
        {
            var name = _receipt.AvailablePrinterName();
            return Ok(new { success = true, data = new { printer = name, available = name != null } });
        }

        /// <summary>چاپ فیش تراکنش — فقط وقتی پرینتر POS نصب باشد</summary>
        [HttpPost]
        [Route("api/Desk/PrintReceipt")]
        public async Task<IActionResult> PrintReceipt([FromBody] ReceiptDoc? req)
        {
            if (req == null)
                return Ok(new { success = false, printed = false, message = "محتوای فیش ارسال نشده است" });

            if (_receipt.AvailablePrinterName() == null)
                return Ok(new { success = false, printed = false, message = "پرینتر POS روی سیستم نصب نیست" });

            var doc = new ReceiptDoc
            {
                ClubName = await _helper.GetClubNameAsync(),
                Title = Clip(req.Title, 60, "رسید"),
                Party = Clip(req.Party, 60),
                PartySub = Clip(req.PartySub, 60),
                Total = Math.Max(0, req.Total),
                TotalLabel = Clip(req.TotalLabel, 30),
                RefNo = Clip(req.RefNo, 30),
                PaymentMethod = Clip(req.PaymentMethod, 30),
                Note = Clip(req.Note, 120),
                Date = _helper.GetToday(),
                Time = _helper.GetThisTime(),
                Rows = (req.Rows ?? new List<ReceiptRowDto>())
                        .Take(40)
                        .Select(r => new ReceiptRowDto
                        {
                            Name = Clip(r.Name, 70, "-"),
                            Qty = Math.Clamp(r.Qty, 1, 999),
                            Amount = Math.Max(0, r.Amount)
                        })
                        .ToList()
            };

            var (ok, message) = _receipt.Print(doc);
            return Ok(new { success = ok, printed = ok, message });
        }

        private static string? Clip(string? value, int max, string? fallback = null)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            var v = value.Trim();
            return v.Length <= max ? v : v.Substring(0, max);
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
