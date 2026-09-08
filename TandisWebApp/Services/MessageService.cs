using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

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
                    (m.TargetType == 0 && m.SenderMemberID == memberID) ||
                    m.TargetType == 1 ||
                    (m.TargetType == 2 && m.TargetRoleID == roleID) ||
                    (m.TargetType == 3 && m.TargetSportCatID != null && mySports.Contains(m.TargetSportCatID.Value)) ||
                    (m.TargetType == 4 && m.TargetMemberID == memberID)))
                .OrderByDescending(m => m.CreationDateTime)
                .ToListAsync();

            return msgs.Select(m => new MemberMessageRowDto
            {
                MessageID = m.MessageID,
                Direction = m.TargetType == 0 ? "out" : "in",
                Title = m.Title,
                Body = m.Body,
                CreationDate = m.CreationDate,
                CreationTime = m.CreationTime,
                IsRead = m.TargetType == 0 ? true : readIds.Contains(m.MessageID)
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
                _db.MsgReads.Add(new Msg_Read { MessageID = messageID, MemberID = memberID });
                await _db.SaveChangesAsync();
            }
        }

        public async Task SendFromMemberAsync(int memberID, string? title, string body)
        {
            _db.MsgMessages.Add(new Msg_Message
            {
                Title = title,
                Body = body,
                TargetType = 0,
                SenderMemberID = memberID
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
                    m.Title,
                    m.Body,
                    m.CreationDate,
                    m.CreationTime,
                    m.IsSeen
                }).ToListAsync();

            return rows.Select(x => new AdminInboxRowDto
            {
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
            _db.MsgMessages.Add(new Msg_Message
            {
                Title = req.Title,
                Body = req.Body,
                TargetType = req.TargetType,
                TargetRoleID = req.TargetType == 2 ? req.TargetRoleID : null,
                TargetSportCatID = req.TargetType == 3 ? req.TargetSportCatID : null,
                TargetMemberID = req.TargetType == 4 ? req.TargetMemberID : null,
                SenderUserID = userID
            });
            await _db.SaveChangesAsync();
        }

        public async Task ReplyAsync(short? userID, ReplyMessageRequest req)
        {
            var orig = await _db.MsgMessages.FindAsync(req.ReplyToMessageID);
            if (orig == null || orig.SenderMemberID == null) return;

            _db.MsgMessages.Add(new Msg_Message
            {
                Title = req.Title ?? "پاسخ پیام شما",
                Body = req.Body,
                TargetType = 4,
                TargetMemberID = orig.SenderMemberID,
                SenderUserID = userID
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
    }
}