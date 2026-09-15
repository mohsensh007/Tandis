using Microsoft.EntityFrameworkCore;
using System.Timers;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TandisWebApp.Services
{
    public class MessageService
    {
        private readonly FullSportDbContext _db;
        public MessageService(FullSportDbContext db) { _db = db; }

        // ========== سمت عضو ==========
        // ========== لیست رشته‌های ورزشی فعال عضو ==========
        private async Task<List<int>> GetMemberSportCatsAsync(int memberID)
        {
            return await _db.Acc_MemberSports
                .Where(a => a.MemberID == memberID
                         && a.IsActive == true
                         && a.Gen_SportSanse != null
                         && a.Gen_SportSanse.SportCatID != null)
                .Select(a => a.Gen_SportSanse!.SportCatID!.Value)
                .Distinct()
                .ToListAsync();
        }

        public async Task<List<MemberMessageRowDto>> GetMemberMessagesAsync(int memberID, int roleID)
        {
            var mySports = await GetMemberSportCatsAsync(memberID);
            var readIds = await _db.MsgReads.Where(r => r.MemberID == memberID)
                .Select(r => r.MessageID).ToListAsync();

            var msgs = await _db.MsgMessages
                .Where(m => m.IsActive && (
                    (m.TargetType == 0 && m.SenderMemberID == memberID) ||        // ارسالی به مدیریت
                    m.TargetType == 1 ||
                    (m.TargetType == 2 && m.TargetRoleID == roleID) ||
                    (m.TargetType == 3 && m.TargetSportCatID != null && mySports.Contains(m.TargetSportCatID.Value)) ||
                    (m.TargetType == 4 && m.TargetMemberID == memberID) ||        // دریافتی (از مدیر یا مربی)
                    (m.TargetType == 4 && m.SenderMemberID == memberID)))         // ✅ جدید: ارسالی به مربی
                .OrderByDescending(m => m.CreationDateTime)
                .ToListAsync();

            // ✅ نام طرف مقابل (مربی) برای پیام‌های گفتگوی خصوصی
            var peerIds = msgs
                .Where(m => m.TargetType == 4)
                .Select(m => m.SenderMemberID == memberID ? m.TargetMemberID : m.SenderMemberID)
                .Where(x => x != null)
                .Select(x => x!.Value)
                .Distinct().ToList();

            var peerNames = peerIds.Count == 0
                ? new Dictionary<int, string>()
                : await (from gm in _db.Gen_Members
                         join gp in _db.Gen_Persons on gm.PersonID equals gp.PersonID
                         where peerIds.Contains(gm.MemberID)
                         select new { gm.MemberID, gp.FullName })
                    .ToDictionaryAsync(x => x.MemberID, x => x.FullName ?? "");

            return msgs.Select(m =>
            {
                var isOut = m.TargetType == 0 || m.SenderMemberID == memberID;
                int? peerId = m.TargetType == 4
                    ? (m.SenderMemberID == memberID ? m.TargetMemberID : m.SenderMemberID)
                    : null;

                return new MemberMessageRowDto
                {
                    MessageID = m.MessageID,
                    Direction = isOut ? "out" : "in",
                    Title = m.Title,
                    Body = m.Body,
                    CreationDate = m.CreationDate,
                    CreationTime = m.CreationTime,
                    IsRead = isOut ? true : readIds.Contains(m.MessageID),
                    PeerMemberID = peerId,
                    PeerName = peerId != null && peerNames.TryGetValue(peerId.Value, out var pn) ? pn : null
                };
            }).ToList();
        }

        public async Task<int> GetMemberUnreadCountAsync(int memberID, int roleID)
        {
            var mySports = await GetMemberSportCatsAsync(memberID);
            var readIds = await _db.MsgReads.Where(r => r.MemberID == memberID)
                                            .Select(r => r.MessageID).ToListAsync();
            return await _db.MsgMessages.CountAsync(m => m.IsActive && m.TargetType != 0 && (
                m.TargetType == 1 ||
                (m.TargetType == 2 && m.TargetRoleID == roleID) ||
                (m.TargetType == 3 && m.TargetSportCatID != null && mySports.Contains(m.TargetSportCatID.Value)) ||
                (m.TargetType == 4 && m.TargetMemberID == memberID)) &&
                !readIds.Contains(m.MessageID));
        }

        public async Task MarkReadAsync(long messageID, int memberID)
        {
            var exists = await _db.MsgReads.AnyAsync(r => r.MessageID == messageID && r.MemberID == memberID);
            if (!exists)
            {
                _db.MsgReads.Add(new Msg_Read
                {
                    MessageID = messageID,
                    MemberID = memberID,
                    ReadDateTime = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }
        }

        public async Task SendFromMemberAsync(int memberID, string? title, string body)
        {
            var (now, date, time) = NowShamsi();
            _db.MsgMessages.Add(new Msg_Message
            {
                Title = title,
                Body = body,
                TargetType = 0,
                SenderMemberID = memberID,
                IsActive = true,
                CreationDateTime = now,
                CreationDate = date,       // ✅ تاریخ شمسی
                CreationTime = time        // ✅ ساعت
            });
            await _db.SaveChangesAsync();
        }

        // ========== سمت ادمین ==========

        public async Task<List<AdminInboxRowDto>> GetAdminInboxAsync(short shiftID)
        {
            var rows = await (
                from m in _db.MsgMessages
                where m.TargetType == 0 && m.SenderMemberID != null
                join gm in _db.Gen_Members on m.SenderMemberID equals gm.MemberID
                join gp in _db.Gen_Persons on gm.PersonID equals gp.PersonID into pj
                from gp in pj.DefaultIfEmpty()
                where gm.ShiftID == shiftID
                orderby m.CreationDateTime descending
                select new
                {
                    m.MessageID,
                    gm.MemberID,
                    FullName = gp != null ? gp.FullName : "",
                    gp.Mobile,
                    gm.PersonID,          // ✅
                    m.Title,
                    m.Body,
                    m.CreationDate,
                    m.CreationTime,
                    m.IsSeen
                }).ToListAsync();

            return rows.Select(x => new AdminInboxRowDto
            {
                PersonID = x.PersonID ?? 0,
                MessageID = x.MessageID,
                SenderName = string.IsNullOrEmpty(x.FullName) ? "-" : x.FullName,
                MemberCode = x.MemberID.ToString("##,###,###").Replace(',', '،'),
                Mobile = x.Mobile,
                Title = x.Title,
                Body = x.Body,
                CreationDate = x.CreationDate,
                CreationTime = x.CreationTime,
                IsSeen = x.IsSeen
            }).ToList();
        }

        public async Task<int> GetAdminUnreadCountAsync(short shiftID)
            => await _db.MsgMessages.CountAsync(m => m.TargetType == 0 && !m.IsSeen &&
                m.SenderMemberID != null &&
                _db.Gen_Members.Any(gm => gm.MemberID == m.SenderMemberID && gm.ShiftID == shiftID));

        public async Task MarkSeenAsync(long messageID)
        {
            var m = await _db.MsgMessages.FindAsync(messageID);
            if (m != null && !m.IsSeen)
            {
                m.IsSeen = true;
                await _db.SaveChangesAsync();
            }
        }

        public async Task SendFromAdminAsync(short? userID, SendMessageRequest req)
        {
            var (now, date, time) = NowShamsi();
            _db.MsgMessages.Add(new Msg_Message
            {
                Title = req.Title,
                Body = req.Body,
                TargetType = req.TargetType,
                TargetRoleID = req.TargetType == 2 ? req.TargetRoleID : null,
                TargetSportCatID = req.TargetType == 3 ? req.TargetSportCatID : null,
                TargetMemberID = req.TargetType == 4 ? req.TargetMemberID : null,
                SenderUserID = userID,
                IsActive = true,
                CreationDateTime = now,    // ✅ فیکس خطا
                CreationDate = date,       // ✅ تاریخ شمسی
                CreationTime = time        // ✅ ساعت
            });
            await _db.SaveChangesAsync();
        }

        public async Task ReplyAsync(short? userID, ReplyMessageRequest req)
        {
            var orig = await _db.MsgMessages.FindAsync(req.ReplyToMessageID);
            if (orig == null || orig.SenderMemberID == null) return;

            var (now, date, time) = NowShamsi();
            _db.MsgMessages.Add(new Msg_Message
            {
                Title = req.Title ?? "پاسخ پیام شما",
                Body = req.Body,
                TargetType = 4,
                TargetMemberID = orig.SenderMemberID,
                SenderUserID = userID,
                IsActive = true,
                CreationDateTime = now,    // ✅
                CreationDate = date,       // ✅
                CreationTime = time        // ✅
            });
            orig.IsSeen = true;
            await _db.SaveChangesAsync();
        }
        // ========== تشخیص عضو جاری از روی کلیم‌ها ==========
        public async Task<(int memberID, int roleID)> ResolveCurrentMemberAsync(string? memberIdClaim, string? userName)
        {
            // حالت ۱: کلیم MemberID
            if (int.TryParse(memberIdClaim, out var id) && id > 0)
            {
                var m = await _db.Gen_Members.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.MemberID == id);
                if (m != null) return (m.MemberID, m.RoleID ?? 1);
            }

            // حالت ۲ (fallback): کد ملی از Name کاربر
            var nc = userName ?? "";
            var m2 = await (from gm in _db.Gen_Members
                            join gp in _db.Gen_Persons on gm.PersonID equals gp.PersonID
                            where gp.NationalCode == nc
                            select new { gm.MemberID, gm.RoleID })
                           .FirstOrDefaultAsync();

            return m2 != null ? (m2.MemberID, m2.RoleID ?? 1) : (0, 1);
        }
        // ========== گزینه‌های فرم ارسال پیام ==========
        public async Task<List<RoleOptionDto>> GetRoleOptionsAsync()
            => await _db.Set<Gen_PersonRole>().AsNoTracking()
                .OrderBy(r => r.RoleID)
                .Select(r => new RoleOptionDto { RoleID = r.RoleID, RoleDesc = r.RoleDesc ?? "" })
                .ToListAsync();

        public async Task<List<SportOptionDto>> GetSportOptionsAsync()
            => await _db.Set<Gen_Sport_Category>().AsNoTracking()
                .Where(s => s.IsActive == true)
                .OrderBy(s => s.SportCatID)
                .Select(s => new SportOptionDto { SportCatID = s.SportCatID, SportName = s.SportName ?? "" })
                .ToListAsync();

        public async Task<List<MemberOptionDto>> SearchMembersAsync(short shiftID, string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return new List<MemberOptionDto>();

            var key = q.Trim();
            return await (
                from gm in _db.Gen_Members
                join gp in _db.Gen_Persons on gm.PersonID equals gp.PersonID
                where gm.ShiftID == shiftID
                   && ((gp.FullName ?? "").Contains(key)
                       || (gp.NationalCode ?? "").Contains(key)
                       || (gm.CardNo != null && gm.CardNo.Contains(key)))
                orderby gp.FullName
                select new MemberOptionDto
                {
                    MemberID = gm.MemberID,
                    FullName = gp.FullName ?? "-",
                    MemberCode = gm.MemberID.ToString()
                }
            ).Take(10).ToListAsync();
        }
        // ========== کمکی: تاریخ شمسی + ساعت + زمان جاری ==========
        private static (DateTime now, string date, string time) NowShamsi()
        {
            var pc = new System.Globalization.PersianCalendar();
            var now = DateTime.Now;
            return (
                now,
                $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00}",
                now.ToString("HH:mm:ss")
            );
        }
        // ========== جزئیات یک پیام (برای صفحه نمایش کامل) ==========
        public async Task<AdminInboxRowDto?> GetMessageDetailAsync(long messageID)
        {
            var x = await (
                from m in _db.MsgMessages
                where m.MessageID == messageID && m.TargetType == 0 && m.SenderMemberID != null
                join gm in _db.Gen_Members on m.SenderMemberID equals gm.MemberID
                join gp in _db.Gen_Persons on gm.PersonID equals gp.PersonID into pj
                from gp in pj.DefaultIfEmpty()
                select new
                {
                    m.MessageID,
                    gm.MemberID,
                    gm.PersonID,
                    FullName = gp != null ? gp.FullName : "",
                    gp.Mobile,
                    m.Title,
                    m.Body,
                    m.CreationDate,
                    m.CreationTime,
                    m.IsSeen
                }).FirstOrDefaultAsync();

            if (x == null) return null;

            return new AdminInboxRowDto
            {
                PersonID = x.PersonID ?? 0,
                MessageID = x.MessageID,
                SenderName = string.IsNullOrEmpty(x.FullName) ? "-" : x.FullName,
                MemberCode = x.MemberID.ToString("##,###,###").Replace(',', '،'),
                Mobile = x.Mobile,
                Title = x.Title,
                Body = x.Body,
                CreationDate = x.CreationDate,
                CreationTime = x.CreationTime,
                IsSeen = x.IsSeen,
                
            };
        }
        // ========== سمت عضو: چت با مربی ==========

        /// <summary>لیست مربی‌های عضو (از ثبت‌نام‌های فعال)</summary>
        public async Task<List<MemberCoachDto>> GetMemberCoachesAsync(int memberID)
        {
            var coaches = await (
                from a in _db.Acc_MemberSports
                where a.MemberID == memberID && a.IsActive == true
                      && a.Gen_SportSanse != null && a.Gen_SportSanse.CoachMemberID != null
                join cm in _db.Gen_Members on a.Gen_SportSanse.CoachMemberID equals cm.MemberID
                join cp in _db.Gen_Persons on cm.PersonID equals cp.PersonID
                select new { cm.MemberID, cp.FullName }
            ).Distinct().ToListAsync();

            var result = new List<MemberCoachDto>();
            foreach (var c in coaches)
            {
                var last = await _db.MsgMessages
                    .Where(m => m.IsActive && m.TargetType == 4 &&
                                ((m.SenderMemberID == c.MemberID && m.TargetMemberID == memberID) ||
                                 (m.SenderMemberID == memberID && m.TargetMemberID == c.MemberID)))
                    .OrderByDescending(m => m.CreationDateTime)
                    .Select(m => new { m.Body, m.CreationDate, m.CreationTime })
                    .FirstOrDefaultAsync();

                var unread = await _db.MsgMessages
                    .CountAsync(m => m.IsActive && m.TargetType == 4 &&
                                     m.SenderMemberID == c.MemberID && m.TargetMemberID == memberID &&
                                     !_db.MsgReads.Any(r => r.MessageID == m.MessageID && r.MemberID == memberID));

                result.Add(new MemberCoachDto
                {
                    CoachMemberID = c.MemberID,
                    CoachName = c.FullName ?? "",
                    LastMessage = last?.Body,
                    LastMessageDate = last?.CreationDate,
                    LastMessageTime = last?.CreationTime,
                    UnreadCount = unread
                });
            }
            return result.OrderByDescending(r => r.LastMessageDate).ToList();
        }

        /// <summary>متن گفتگوی عضو با یک مربی + علامت‌گذاری خوانده</summary>
        public async Task<CoachChatDto?> GetMemberCoachChatAsync(int memberID, int coachMemberID)
        {
            var related = await _db.Acc_MemberSports
                .AnyAsync(a => a.MemberID == memberID && a.Gen_SportSanse != null && a.Gen_SportSanse.CoachMemberID == coachMemberID);
            if (!related) return null;

            var coachName = await (
                from cm in _db.Gen_Members
                join cp in _db.Gen_Persons on cm.PersonID equals cp.PersonID
                where cm.MemberID == coachMemberID
                select cp.FullName
            ).FirstOrDefaultAsync() ?? "";

            var messages = await _db.MsgMessages
                .Where(m => m.IsActive && m.TargetType == 4 &&
                            ((m.SenderMemberID == coachMemberID && m.TargetMemberID == memberID) ||
                             (m.SenderMemberID == memberID && m.TargetMemberID == coachMemberID)))
                .OrderBy(m => m.CreationDateTime)
                .Select(m => new CoachMessageItemDto
                {
                    MessageID = m.MessageID,
                    IsFromMe = m.SenderMemberID == memberID,
                    Title = m.Title ?? "",
                    Body = m.Body,
                    CreationDate = m.CreationDate ?? "",
                    CreationTime = m.CreationTime ?? ""
                }).ToListAsync();

            var unreadIds = await _db.MsgMessages
                .Where(m => m.IsActive && m.TargetType == 4 &&
                            m.SenderMemberID == coachMemberID && m.TargetMemberID == memberID &&
                            !_db.MsgReads.Any(r => r.MessageID == m.MessageID && r.MemberID == memberID))
                .Select(m => m.MessageID).ToListAsync();

            if (unreadIds.Count > 0)
            {
                foreach (var id in unreadIds)
                    _db.MsgReads.Add(new Msg_Read { MessageID = id, MemberID = memberID, ReadDateTime = DateTime.Now });
                await _db.SaveChangesAsync();
            }

            return new CoachChatDto { StudentMemberID = coachMemberID, StudentName = coachName, Messages = messages };
        }

        /// <summary>ارسال پیام از عضو به مربی</summary>
        public async Task<bool> SendToCoachAsync(int memberID, int coachMemberID, string? title, string body)
        {
            var related = await _db.Acc_MemberSports
                .AnyAsync(a => a.MemberID == memberID && a.Gen_SportSanse != null && a.Gen_SportSanse.CoachMemberID == coachMemberID);
            if (!related) return false;

            var pc = new System.Globalization.PersianCalendar();
            var now = DateTime.Now;

            _db.MsgMessages.Add(new Msg_Message
            {
                Title = title,
                Body = body,
                TargetType = 4,
                TargetMemberID = coachMemberID,
                SenderMemberID = memberID,
                IsActive = true,
                CreationDateTime = now,
                CreationDate = $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00}",
                CreationTime = now.ToString("HH:mm:ss")
            });
            await _db.SaveChangesAsync();
            return true;
        }
    }
}