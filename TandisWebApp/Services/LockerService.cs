using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using CrpCommunications;
using CrpCommunications.Commands;
using CrpCommunications.ConnectionsStatus;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس باز کردن کمد رختکن — ارتباط مستقیم با کنترلر کمد.
    ///
    /// دو نسل کنترلر داریم (دقیقاً مطابق منطق Traffic.exe):
    ///   • Version = ۸/۱۰ (قدیمی): پروتکل C_pc_sg با کتابخانه‌ی ProcondC8.dll
    ///       Init_Ethernet/Init_Serial → Ack → Output_Exe
    ///   • Version = ۳۰ (جدید): پروتکل CrpCommunications با کتابخانه‌ی CrpCommunications.dll
    ///       (همان DLL که ITPanel V2.3.6 و Traffic استفاده می‌کنند)
    ///       GetKey/AuthenticateKey → OpenUdp(23, NetPort) → ControllerInstantiation
    ///       → AreYouThere → OpenLock(lock=BoxNo, delay=0, duration=DelayTime, justOpen=true)
    ///
    /// اطلاعات کنترلر در جدول Gen_LlockerRoom است:
    ///   ControllerIndex , ControllerID , IpAddress , NetPort , ServerIP , Version , IsOnline
    ///
    /// ⚠️ کتابخانه‌ها در Libs پروژه‌اند. نسخه‌ی CrpCommunications از Tools/ITPanel/V2.3.6 آمده؛
    ///     فلگ 32BITREQUIRED آن پاک شده تا روی .NET 10 (x64) لود شود (کد IL خالص است).
    /// ⚠️ آنتی‌دیباگ آن هم حذف شده: کانستراکتور <Module> آن با call به R.F() (پرتابException
    ///     وقتی دیباگر وصل است) اجرا می‌شد و کل بارگذاری کتابخانه را می‌شکست (TypeInitializationException).
    ///     پچ: آن call با nop جایگزین شده (ابزار crppatch). نسخه‌ی اصلی: Libs/CrpCommunications.dll.orig-backup
    /// </summary>
    public class LockerService
    {
        private readonly FullSportDbContext _db;
        private readonly AthleteService _athlete;
        private readonly IConfiguration _cfg;
        private static readonly SemaphoreSlim _commLock = new(1, 1);

        public LockerService(FullSportDbContext db, AthleteService athlete, IConfiguration cfg)
        {
            _db = db;
            _athlete = athlete;
            _cfg = cfg;
        }

        /// <summary>لیست رختکن‌ها + کمدهای فعال هر کدام (برای کامپوبوکس صفحه)</summary>
        public async Task<List<LockerRoomOptionDto>> GetRoomsAsync()
        {
            var rooms = await _db.Gen_LockerRooms.AsNoTracking().ToListAsync();
            var boxes = await _db.Gen_Boxes.AsNoTracking().Where(b => b.IsActive != false).ToListAsync();

            return rooms
                .OrderBy(r => r.LockerRoomID)
                .Select(r => new LockerRoomOptionDto
                {
                    LockerRoomID = r.LockerRoomID,
                    LockerRoomName = r.LockerRoomName ?? $"رختکن {r.LockerRoomID}",
                    IsOnline = r.IsOnline == true,
                    // ✅ فقط وجود آدرس کنترلر مهم است؛ Version کنترلرهای جدید ۳۰ است نه ۸
                    HasController = r.ControllerID != null && r.ControllerIndex != null,
                    Transport = string.IsNullOrWhiteSpace(r.IpAddress) ? "Serial" : "UDP",
                    Boxes = boxes
                        .Where(b => b.LockerRoomID == r.LockerRoomID)
                        .OrderBy(b => b.BoxNo)
                        .Select(b => new LockerBoxOptionDto
                        {
                            BoxID = b.BoxID,
                            BoxNo = b.BoxNo ?? 0,
                            RadifNo = b.RadifNo
                        })
                        .ToList()
                })
                .ToList();
        }

        /// <summary>
        /// باز کردن یک کمد. ابتدا Ack می‌زنیم (مثل Traffic) بعد دستور Output_Exe را می‌فرستیم.
        /// </summary>
        public async Task<LockerOpenResultDto> OpenBoxAsync(int memberID, short lockerRoomID, short boxNo)
        {
            var room = await _db.Gen_LockerRooms.AsNoTracking()
                .FirstOrDefaultAsync(r => r.LockerRoomID == lockerRoomID);
            if (room == null)
                return await Fail(memberID, lockerRoomID, boxNo, "رختکن پیدا نشد.");

            if (room.IsOnline != true)
                return await Fail(memberID, lockerRoomID, boxNo, "این رختکن آفلاین است.");

            if (room.ControllerID == null)
                return await Fail(memberID, lockerRoomID, boxNo, "آدرس کنترلر (ControllerID) این رختکن در دیتابیس تنظیم نشده است.");

            if (room.ControllerIndex == null || room.ControllerIndex > 32)
                return await Fail(memberID, lockerRoomID, boxNo, "شماره‌ی کنترلر (ControllerIndex) در دیتابیس تنظیم نشده.");

            var box = await _db.Gen_Boxes.AsNoTracking()
                .FirstOrDefaultAsync(b => b.LockerRoomID == lockerRoomID && b.BoxNo == boxNo);
            if (box == null)
                return await Fail(memberID, lockerRoomID, boxNo, "شماره‌ی کمد در این رختکن وجود ندارد.");

            int duration = box.DelayTime ?? 15; // مدت باز ماندن کمد (ثانیه) — مثل DelayTime در Traffic
            LockerOpenResultDto result;

            // 🌐 کنترلر فقط بسته‌های IP سیستمِ ServerIP (۱۹۲.۱۶۸.۱.۱۷۲) را قبول می‌کند؛
            //    اگر این برنامه روی سیستم دیگری اجرا می‌شود، فرمان باید از طریق سرور رله شود.
            if (!string.IsNullOrWhiteSpace(room.ServerIP) && !IsLocalIp(room.ServerIP))
            {
                result = await RelayOpenAsync(room, boxNo, duration);
            }
            else
            {
                // کنترلر یک خط ارتباطی مشترک (سریال/UDP) دارد — فرمان‌ها را صف می‌کنیم
                await _commLock.WaitAsync();
                try
                {
                    result = await Task.Run(() => SendCommand(room, boxNo, duration));
                }
                finally
                {
                    _commLock.Release();
                }
            }
            await _athlete.LogLockerOpenAsync(memberID, lockerRoomID, boxNo, result.Success, result.Message);

            result.Message = result.Success
                ? $"✔ دستور باز شدن کمد {boxNo} ارسال شد."
                : $"✖ کمد {boxNo} باز نشد — {result.Message}";
            return result;
        }

        /// <summary>هسته‌ی ارتباط با کنترلر (دقیقاً مطابق منطق Traffic.exe)</summary>
        private LockerOpenResultDto SendCommand(Models.Gen_LockerRoom room, short boxNo, int duration)
        {
            // کنترلر نسل جدید (Version=30) → پروتکل CrpCommunications (ITPanel V2.3.6 / Traffic)
            if (room.Version == 30 && !string.IsNullOrWhiteSpace(room.IpAddress))
                return SendCommandV30(room, boxNo, duration);

            // ---------- کنترلر قدیمی (Version 8/10) → پروتکل C_pc_sg (ProcondC8) ----------
            var comm = new ProcondC8.Comm();
            int idx = room.ControllerIndex!.Value;
            byte addr = (byte)(room.ControllerID ?? 0);

            // 1) معرفی کنترلر: شبکه (UDP) یا سریال
            ProcondC8.enResult initRes;
            if (string.IsNullOrWhiteSpace(room.IpAddress))
            {
                var port = (room.CntrollerComPort ?? "").Trim();
                if (port.Length == 0)
                    return new LockerOpenResultDto { Success = false, Message = "پورت کنترلر (CntrollerComPort) در دیتابیس خالی است." };

                // "3" — همان مقداری که Traffic.exe برای baudRate می‌فرستد
                initRes = comm.Setting.Init_Serial(idx, addr, "3", port) ? ProcondC8.enResult.OK : ProcondC8.enResult.Wrong;
            }
            else
            {
                if (room.NetPort == null || room.NetPort == 0)
                    return new LockerOpenResultDto { Success = false, Message = "پورت شبکه‌ی کنترلر (NetPort) در دیتابیس خالی است." };

                initRes = comm.Setting.Init_Ethernet(idx, addr, room.IpAddress, (ushort)room.NetPort.Value, "UDP");
            }

            if (initRes != ProcondC8.enResult.OK)
                return new LockerOpenResultDto { Success = false, Message = "مقداردهی ارتباط کنترلر ناموفق بود." };

            // 2) تست ارتباط (Ack مثل Traffic)
            var ack = comm.Ack((byte)idx);
            if (ack != ProcondC8.enResult.ReplyOk)
                return new LockerOpenResultDto
                {
                    Success = false,
                    Message = "کنترلر پاسخ نمی‌دهد.",
                    Detail = ack.ToString()
                };

            // 3) فرمان باز کردن خروجی (کمد)
            string str = "";
            byte res = 0;
            var r = comm.Output_Exe((byte)idx, boxNo, 0, ref str, ref res);

            bool ok = r == ProcondC8.enResult.ReplyOk || r == ProcondC8.enResult.ReplyData;
            return new LockerOpenResultDto
            {
                Success = ok,
                Message = ok ? "فرمان ارسال شد." : "کنترلر فرمان را نپذیرفت.",
                Detail = string.IsNullOrEmpty(str) ? r.ToString() : $"{r} / {str}"
            };
        }

        // ============================================================
        //  کنترلر نسل جدید (Version=30) — پروتکل CrpCommunications
        //  ترتیب دقیقاً مطابق Traffic.exe:
        //    GetKey → AuthenticateKey → OpenUdp(23, NetPort) → Protocol=UDP
        //    → ControllerInstantiation → AreYouThere → OpenLock
        // ============================================================

        // ⚠️ پورت محلی UDP: نباید 23 باشد! Traffic.exe روی همین سرور پورت 23 را bind کرده و
        //    جواب‌های کنترلر را می‌گیرد (AreYouThere=false). تست زنده: پورت 23232 جواب می‌گیرد.
        private const int CrpUdpLocalPort = 23232;

        // وضعیت ارتباط (کتابخانه state استاتیک دارد — مثل Traffic.exe کل برنامه یک نمونه دارد)
        private static readonly ItTandisLockerManagement _crpLocker = new ItTandisLockerManagement();
        private static readonly BasicCommands _crpBasic = new BasicCommands();
        private static readonly Connections _crpConn = new Connections();
        private static bool _crpAuthed;
        private static bool _crpUdpOpened;

        /// <summary>
        /// دروازه‌ی احراز هویت داخلی کتابخانه: GetKey یک کلید تصادفی می‌سازد و نتیجه‌ی مورد انتظار را
        /// در تنها فیلد استاتیک int کلاس ذخیره می‌کند؛ AuthenticateKey باید همان مقدار را بگیرد.
        /// (Traffic.exe همین کار را با فرمول (key ^ ثابت) / ثابت انجام می‌دهد، اما ثابت‌ها بین
        /// نسخه‌های کتابخانه فرق دارد — بنابراین مقدار ذخیره‌شده را مستقیم می‌خوانیم.)
        /// </summary>
        private static bool EnsureCrpAuthed(out string? err)
        {
            err = null;
            if (_crpAuthed) return true;
            try
            {
                _crpLocker.GetKey();
                FieldInfo? keyField = typeof(ItTandisLockerManagement)
                    .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(f => f.FieldType == typeof(int));
                if (keyField == null)
                {
                    err = "ساختار داخلی کتابخانه‌ی CrpCommunications تغییر کرده است.";
                    return false;
                }
                int expected = (int)keyField.GetValue(null)!;
                _crpAuthed = _crpLocker.AuthenticateKey(expected);
                if (!_crpAuthed) err = "احراز هویت کلید کنترلر (AuthenticateKey) ناموفق بود.";
                return _crpAuthed;
            }
            catch (Exception ex)
            {
                err = "خطا در احراز هویت کنترلر: " + ex.Message;
                return false;
            }
        }

        /// <summary>هسته‌ی ارتباط با کنترلر نسل ۳۰ (UDP روی NetPort، پورت محلی ۲۳ مثل Traffic)</summary>
        private LockerOpenResultDto SendCommandV30(Models.Gen_LockerRoom room, short boxNo, int duration)
        {
            if (room.NetPort == null || room.NetPort == 0)
                return new LockerOpenResultDto { Success = false, Message = "پورت شبکه‌ی کنترلر (NetPort) در دیتابیس خالی است." };
            if (room.ControllerID == null)
                return new LockerOpenResultDto { Success = false, Message = "آدرس کنترلر (ControllerID) در دیتابیس تنظیم نشده." };

            if (!EnsureCrpAuthed(out var authErr))
                return new LockerOpenResultDto { Success = false, Message = authErr };

            byte idx = (byte)room.ControllerIndex!.Value;
            byte cid = (byte)room.ControllerID.Value;
            int netPort = room.NetPort.Value;

            try
            {
                // 1) سوکت UDP (یک بار برای کل برنامه — مثل Traffic: پورت محلی 23)
                if (!_crpUdpOpened)
                {
                    try { _crpBasic.OpenUdp(CrpUdpLocalPort, netPort); }
                    catch { _crpBasic.OpenUdp(0, netPort); } // اگر پورت هم اشغال بود → پورت دلخواه سیستم
                    _crpConn.Protocol = Protocols.UDP;
                    _crpConn.IsOpen = IsOpen.Open;
                    _crpUdpOpened = true;
                }

                // 2) معرفی کنترلر (آدرس + پورت)
                IPAddress ip = IPAddress.Parse(room.IpAddress!);
                EndPoint ep = new IPEndPoint(ip, netPort);
                _crpBasic.ControllerInstantiation(idx, cid, ip, ep);

                // 3) تست ارتباط (مثل دکمه‌ی تست ITPanel/Traffic)
                if (!_crpBasic.AreYouThere(cid, Protocols.UDP, out byte devId, idx))
                    return new LockerOpenResultDto
                    {
                        Success = false,
                        Message = "کنترلر پاسخ نمی‌دهد (خاموش یا قطع اتصال).",
                        Detail = "AreYouThere=false"
                    };
                _crpConn.ConnectedDeviceID = devId;

                // 4) فرمان باز کردن کمد (justOpen=true — همان دکمه‌ی باز کردن Traffic)
                byte dur = (byte)Math.Clamp(duration <= 0 ? 15 : duration, 1, 255);
                byte r = _crpLocker.OpenLock((byte)boxNo, 0, dur, true, false, Protocols.UDP, idx);
                bool ok = r == 1; // 1 = پاسخ DONE از کنترلر
                return new LockerOpenResultDto
                {
                    Success = ok,
                    Message = ok ? "فرمان ارسال شد." : "کنترلر فرمان را نپذیرفت.",
                    Detail = "OpenLock=" + r
                };
            }
            catch (Exception ex)
            {
                return new LockerOpenResultDto
                {
                    Success = false,
                    Message = "خطا در ارتباط با کنترلر نسل ۳۰: " + ex.Message
                };
            }
        }

        // ============================================================
        //  رله‌ی فرمان: ارسال از سیستمی که IP آن به‌عنوان ServerIP کنترلر تعریف شده
        // ============================================================

        /// <summary>آیا این IP متعلق به همین سیستم است؟ (فقط IPهای واقعی کارت‌های شبکه)</summary>
        private static bool IsLocalIp(string ip)
        {
            if (!IPAddress.TryParse(ip.Trim(), out var target)) return false;
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.Equals(target)) return true;
            return false;
        }

        /// <summary>فرمان باز کردن کمد را از طریق سرورِ کنترلر (آدرس از ServerIP دیتابیس) رله می‌کند.</summary>
        private async Task<LockerOpenResultDto> RelayOpenAsync(Models.Gen_LockerRoom room, short boxNo, int duration)
        {
            int port = 5104;
            int.TryParse(_cfg["LockerRelay:Port"], out port);
            if (port <= 0 || port > 65535) port = 5104;
            string? secret = _cfg["LockerRelay:Secret"];
            string url = $"http://{room.ServerIP}:{port}/api/LockerRelay/Open";

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(new LockerRelayRequestDto
                    {
                        LockerRoomID = room.LockerRoomID,
                        BoxNo = boxNo,
                        Duration = duration
                    })
                };
                if (!string.IsNullOrWhiteSpace(secret))
                    req.Headers.Add("X-Locker-Relay-Key", secret);

                var resp = await http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var dto = await resp.Content.ReadFromJsonAsync<LockerOpenResultDto>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return dto ?? new LockerOpenResultDto
                {
                    Success = false,
                    Message = "پاسخ خالی از سرور کنترلر (" + room.ServerIP + ")."
                };
            }
            catch (Exception ex)
            {
                return new LockerOpenResultDto
                {
                    Success = false,
                    Message = $"ارسال فرمان از طریق سرور {room.ServerIP} ممکن نشد — آیا برنامه روی آن سیستم در حال اجراست؟",
                    Detail = ex.Message
                };
            }
        }

        /// <summary>✅ endpoint رله (LockerRelayController): اجرای مستقیم فرمان روی سیستمِ ServerIP — بدون رله‌ی مجدد.</summary>
        public async Task<LockerOpenResultDto> OpenRelayedAsync(short lockerRoomID, short boxNo, int duration)
        {
            var room = await _db.Gen_LockerRooms.AsNoTracking()
                .FirstOrDefaultAsync(r => r.LockerRoomID == lockerRoomID);
            if (room == null)
                return new LockerOpenResultDto { Success = false, Message = "رختکن پیدا نشد." };

            await _commLock.WaitAsync();
            try
            {
                return await Task.Run(() => SendCommand(room, boxNo, duration <= 0 ? 15 : duration));
            }
            finally
            {
                _commLock.Release();
            }
        }

        private async Task<LockerOpenResultDto> Fail(int memberID, short roomID, short boxNo, string msg)
        {
            try { await _athlete.LogLockerOpenAsync(memberID, roomID, boxNo, false, msg); } catch { }
            return new LockerOpenResultDto { Success = false, Message = msg };
        }
    }
}
