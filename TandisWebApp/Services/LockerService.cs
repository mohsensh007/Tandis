using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس باز کردن کمد رختکن — ارتباط مستقیم با کنترلر کمد.
    ///
    /// پروتکل از روی برنامه‌ی اصلی باشگاه (Traffic.exe در پوشه‌ی 14050311) استخراج شده:
    ///   - اطلاعات کنترلر در جدول Gen_LlockerRoom است:
    ///       ControllerIndex , ControllerID , IpAddress , NetPort , CntrollerComPort , Version , IsOnline
    ///   - کتابخانه‌ی مورد استفاده‌ی Traffic: ProcondC8.dll (پروتکل C_pc_sg ، همان چیزی که
    ///     SA_DLL هم پیاده می‌کند). ترافیک اصلی: اگر IpAddress پر باشد اتصال شبکه‌ای UDP،
    ///     وگرنه پورت سریال ( baud = "3" همان‌طور که در Traffic.exe هست ).
    ///   - باز کردن کمد: Output_Exe( شماره‌ی کنترلر، شماره‌ی کمد، 0 ) — دقیقاً همان فراخوانی
    ///     Traffic.exe برای دکمه‌ی «باز کردن کمد».
    ///
    /// ⚠️ کنترلر فیزیکی فعلاً وصل نیست؛ کد کامل نوشته شده و به‌محض وصل‌شدن کنترلر قابل تست است.
    /// </summary>
    public class LockerService
    {
        private readonly FullSportDbContext _db;
        private readonly AthleteService _athlete;
        private static readonly SemaphoreSlim _commLock = new(1, 1);

        public LockerService(FullSportDbContext db, AthleteService athlete)
        {
            _db = db;
            _athlete = athlete;
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
                    HasController = r.Version == 8 && r.ControllerID != null,
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

            if (room.Version != 8 || room.ControllerID == null)
                return await Fail(memberID, lockerRoomID, boxNo,
                    "نوع کنترلر این رختکن برای وب‌اپ پیکربندی نشده (Version=8 و ControllerID لازم است).");

            if (room.ControllerIndex == null || room.ControllerIndex > 32)
                return await Fail(memberID, lockerRoomID, boxNo, "شماره‌ی کنترلر (ControllerIndex) در دیتابیس تنظیم نشده.");

            var box = await _db.Gen_Boxes.AsNoTracking()
                .FirstOrDefaultAsync(b => b.LockerRoomID == lockerRoomID && b.BoxNo == boxNo);
            if (box == null)
                return await Fail(memberID, lockerRoomID, boxNo, "شماره‌ی کمد در این رختکن وجود ندارد.");

            LockerOpenResultDto result;
            // کنترلر یک خط ارتباطی مشترک (سریال/UDP) دارد — فرمان‌ها را صف می‌کنیم
            await _commLock.WaitAsync();
            try
            {
                result = await Task.Run(() => SendCommand(room, boxNo));
            }
            finally
            {
                _commLock.Release();
            }
            await _athlete.LogLockerOpenAsync(memberID, lockerRoomID, boxNo, result.Success, result.Message);

            result.Message = result.Success
                ? $"✔ دستور باز شدن کمد {boxNo} ارسال شد."
                : $"✖ کمد {boxNo} باز نشد — {result.Message}";
            return result;
        }

        /// <summary>هسته‌ی ارتباط با کنترلر (دقیقاً مطابق منطق Traffic.exe)</summary>
        private LockerOpenResultDto SendCommand(Models.Gen_LockerRoom room, short boxNo)
        {
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

        private async Task<LockerOpenResultDto> Fail(int memberID, short roomID, short boxNo, string msg)
        {
            try { await _athlete.LogLockerOpenAsync(memberID, roomID, boxNo, false, msg); } catch { }
            return new LockerOpenResultDto { Success = false, Message = msg };
        }
    }
}
