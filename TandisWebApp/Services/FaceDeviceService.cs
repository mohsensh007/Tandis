using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// ارسال دستور «ثبت چهره» به دستگاه تشخیص چهره (تیام)
    ///
    /// معادل btnAddFace_Click در کیوسک (TandisKiosk/UscRegister.cs):
    /// 1) پیدا کردن دستگاه در Gen_GateDevice با TrafficType=9(Kiosk) و DeviceID=60070(Tiam)
    /// 2) نوشتن فلگ در Gen_Setting:
    ///      MemberIDForTrafficApp   = عضو
    ///      TerminalIDForTrafficApp = TerminalNo دستگاه
    ///      ActionForTrafficApp     = "addface"
    ///      ActionValidTime         = زمان اعتبار دستور
    /// 3) Traffic.exe (که روی پورت 8085 لیسن می‌کند) فلگ را می‌خواند و
    ///    دستور را از طریق WebSocket (اتصال برقرارشده توسط دستگاه) به دستگاه می‌فرستد.
    /// </summary>
    public class FaceDeviceService
    {
        /// <summary>MyProvider.DeviceEnum.Tiam</summary>
        private const int TiamDeviceID = 60070;

        /// <summary>MyProvider.TrafficEnum.Kiosk</summary>
        private const short KioskTrafficType = 9;

        private readonly FullSportDbContext _db;
        private readonly ILogger<FaceDeviceService> _logger;

        public FaceDeviceService(FullSportDbContext db, ILogger<FaceDeviceService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>ارسال دستور ثبت چهره برای یک عضو</summary>
        public async Task<ApiResponse<FaceCommandResponse>> SendFaceCaptureAsync(int memberID)
        {
            try
            {
                // === ۱. عضو باید وجود داشته باشد ===
                if (memberID <= 0)
                    return new ApiResponse<FaceCommandResponse> { Success = false, Message = "شناسه عضو نامعتبر است" };

                if (!await _db.Gen_Members.AnyAsync(m => m.MemberID == memberID))
                    return new ApiResponse<FaceCommandResponse> { Success = false, Message = "عضو یافت نشد" };

                // === ۲. دستگاه تشخیص چهره (تیام) ===
                var device = await _db.Gen_GateDevices
                    .Where(d => d.TrafficType == KioskTrafficType && d.DeviceID == TiamDeviceID)
                    .OrderBy(d => d.GateDeviceID)
                    .FirstOrDefaultAsync();

                if (device == null)
                    return new ApiResponse<FaceCommandResponse>
                    {
                        Success = false,
                        Message = "دستگاه تشخیص چهره در Gen_GateDevice تعریف نشده است (TrafficType=9 و DeviceID=60070)"
                    };

                // === ۳. بررسی اجرای سرویس‌ها: اگر Traffic.exe یا FullSport.exe اجرا نباشند، ارسال انجام نمی‌شود ===
                var (trafficOk, fullSportOk, portOpen, host) = CheckServices(device.ServerIP);

                if (!trafficOk || !fullSportOk)
                {
                    var missing = new List<string>();
                    if (!trafficOk) missing.Add("Traffic.exe (سرویس تردد / پورت 8085)");
                    if (!fullSportOk) missing.Add("FullSport.exe (برنامه اصلی)");

                    _logger.LogWarning("ارسال دستور چهره انجام نشد، سرویس در دسترس نیست: {Missing}", string.Join(" و ", missing));

                    return new ApiResponse<FaceCommandResponse>
                    {
                        Success = false,
                        Message = "ارسال دستور چهره انجام نشد؛ ابتدا این برنامه‌ها را اجرا کنید: " + string.Join(" و ", missing),
                        Data = new FaceCommandResponse
                        {
                            MemberID = memberID,
                            ServiceHost = host,
                            TrafficRunning = trafficOk,
                            FullSportRunning = fullSportOk,
                            Port8085Open = portOpen
                        }
                    };
                }

                // === ۴. بررسی اتصال دستگاه در شبکه (غیر بلوکه‌کننده در پیام اصلی) ===
                bool reachable = await IsDeviceReachableAsync(device.IPAddress, device.PortNo);

                // === ۴. وضعیت چهره قبل از ارسال دستور (برای مقایسه بعدی) ===
                var hadFaceBefore = await _db.Gen_Members
                    .Where(m => m.MemberID == memberID)
                    .Select(m => m.FaceTmpl1 != null || m.FaceTmpl2 != null || m.FaceTmpl3 != null)
                    .FirstOrDefaultAsync();

                // === ۵. فلگ دستور در Gen_Setting (معادل کیوسک) ===
                var settings = await _db.Gen_Settings.FirstOrDefaultAsync();
                if (settings == null)
                    return new ApiResponse<FaceCommandResponse> { Success = false, Message = "رکورد Gen_Setting یافت نشد" };

                settings.MemberIDForTrafficApp = memberID;
                settings.TerminalIDForTrafficApp = device.TerminalNo;
                settings.ActionForTrafficApp = "addface";
                settings.ActionValidTime = DateTime.Now.AddMinutes(2);
                await _db.SaveChangesAsync();

                _logger.LogInformation(
                    "دستور ثبت چهره ارسال شد: MemberID={MemberID}, Device={GateDeviceID} ({IP}), TerminalNo={TerminalNo}",
                    memberID, device.GateDeviceID, device.IPAddress, device.TerminalNo);

                return new ApiResponse<FaceCommandResponse>
                {
                    Success = true,
                    Message = reachable
                        ? "دستور ثبت چهره به دستگاه ارسال شد — مقابل دستگاه بایستید تا چهره شما ثبت شود"
                        : "دستور ثبت چهره ارسال شد، اما دستگاه در شبکه پاسخ نمی‌دهد؛ آدرس IP و پورت دستگاه را بررسی کنید",
                    Data = new FaceCommandResponse
                    {
                        MemberID = memberID,
                        GateDeviceID = device.GateDeviceID,
                        DeviceIp = device.IPAddress,
                        DevicePort = device.PortNo,
                        SerialNo = device.SerialNo,
                        TerminalNo = device.TerminalNo,
                        DeviceReachable = reachable,
                        ValidUntil = settings.ActionValidTime,
                        HadFaceBefore = hadFaceBefore,
                        ServiceHost = host,
                        TrafficRunning = trafficOk,
                        FullSportRunning = fullSportOk,
                        Port8085Open = portOpen
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارسال دستور ثبت چهره برای عضو {MemberID}", memberID);
                return new ApiResponse<FaceCommandResponse>
                {
                    Success = false,
                    Message = "خطا در ارسال دستور چهره. لطفاً دوباره تلاش کنید."
                };
            }
        }

        /// <summary>
        /// بررسی اجرای سرویس‌های لازم برای ارسال دستور به دستگاه:
        ///  - Traffic.exe (پورت 8085 که دستگاه به آن وصل می‌شود)
        ///  - FullSport.exe (برنامه اصلی سامانه)
        /// اگر هر کدام در دسترس نباشند، ارسال دستور انجام نمی‌شود.
        /// </summary>
        private static (bool trafficOk, bool fullSportOk, bool port8085Open, string host) CheckServices(string? serverIP)
        {
            string host = string.IsNullOrWhiteSpace(serverIP) ? "127.0.0.1" : serverIP.Trim();

            bool trafficProcess = IsProcessRunning("Traffic");
            bool fullSportProcess = IsProcessRunning("FullSport");
            bool portOpen = IsPortOpen(host, 8085).GetAwaiter().GetResult();

            // سرویس تردد زنده است: یا پروسه Traffic بالاست یا پورت 8085 روی سرور باز است
            bool trafficOk = trafficProcess || portOpen;

            return (trafficOk, fullSportProcess, portOpen, host);
        }

        private static bool IsProcessRunning(string name)
        {
            try
            {
                return Process.GetProcessesByName(name).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> IsPortOpen(string host, int port, int timeoutMs = 1200)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                return await Task.WhenAny(task, Task.Delay(timeoutMs)) == task && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// بررسی وضعیت ثبت چهره یک عضو (برای اطمینان از اینکه آیا چهره واقعاً ثبت شده یا نه)
        /// سیگنال اصلی: پر شدن FaceTmpl1..3 در Gen_Members توسط Traffic.exe پس از دریافت چهره از دستگاه
        /// </summary>
        public async Task<ApiResponse<FaceStatusResponse>> GetFaceStatusAsync(int memberID)
        {
            try
            {
                if (memberID <= 0)
                    return new ApiResponse<FaceStatusResponse> { Success = false, Message = "شناسه عضو نامعتبر است" };

                var member = await _db.Gen_Members
                    .Where(m => m.MemberID == memberID)
                    .Select(m => new { m.FaceTmpl1, m.FaceTmpl2, m.FaceTmpl3 })
                    .FirstOrDefaultAsync();

                if (member == null)
                    return new ApiResponse<FaceStatusResponse>
                    {
                        Success = false,
                        Data = new FaceStatusResponse { MemberID = memberID, MemberFound = false },
                        Message = "عضو یافت نشد"
                    };

                var hasFace = member.FaceTmpl1 != null || member.FaceTmpl2 != null || member.FaceTmpl3 != null;

                var settings = await _db.Gen_Settings.FirstOrDefaultAsync();
                bool pending = settings != null
                    && settings.ActionForTrafficApp == "addface"
                    && settings.MemberIDForTrafficApp == memberID;
                bool anyPending = settings != null && settings.ActionForTrafficApp == "addface";

                DateTime? registeredAt = null;
                try
                {
                    registeredAt = await _db.Database
                        .SqlQuery<DateTime?>(
                            $"SELECT RegisterToDeviceTime FROM dbo.TmpMembersTbl WHERE MemberID = {memberID}")
                        .FirstOrDefaultAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "خواندن TmpMembersTbl ممکن نشد");
                }

                return new ApiResponse<FaceStatusResponse>
                {
                    Success = true,
                    Data = new FaceStatusResponse
                    {
                        MemberID = memberID,
                        MemberFound = true,
                        HasFace = hasFace,
                        CommandPending = pending,
                        CommandConsumed = !pending && !anyPending,
                        RegisteredToDeviceAt = registeredAt
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بررسی وضعیت چهره عضو {MemberID}", memberID);
                return new ApiResponse<FaceStatusResponse>
                {
                    Success = false,
                    Message = "خطا در بررسی وضعیت چهره"
                };
            }
        }

        /// <summary>
        /// بررسی اتصال دستگاه: اول پورت سرویس دستگاه (معمولاً 5005)، بعد پینگ
        /// </summary>
        private static async Task<bool> IsDeviceReachableAsync(string? ip, string? portNo)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return false;

            // --- تست پورت TCP دستگاه (مثال: 192.168.1.14:5005) ---
            if (int.TryParse(portNo, out var port) && port > 0)
            {
                try
                {
                    using var client = new TcpClient();
                    var task = client.ConnectAsync(ip, port);
                    if (await Task.WhenAny(task, Task.Delay(1500)) == task && client.Connected)
                        return true;
                }
                catch
                {
                    // در ادامه پینگ انجام می‌شود
                }
            }

            // --- پینگ ICMP ---
            try
            {
                using var pinger = new System.Net.NetworkInformation.Ping();
                var reply = await pinger.SendPingAsync(ip, 1200);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    }
}
