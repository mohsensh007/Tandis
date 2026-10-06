using System.ComponentModel.DataAnnotations;

namespace TandisWebApp.DTOs
{
    public class AdminInboxRowDto
    {
        public long MessageID { get; set; }
        public int PersonID { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? MemberCode { get; set; }
        public string? Mobile { get; set; }
        public string? Title { get; set; }
        public string Body { get; set; } = string.Empty;
        public string? CreationDate { get; set; }
        public string? CreationTime { get; set; }
        public bool IsSeen { get; set; }
    }

    public class MemberMessageRowDto
    {
        public long MessageID { get; set; }
        public string Direction { get; set; } = "in";   // in = از ادمین، out = به ادمین
        public string? Title { get; set; }
        public string Body { get; set; } = string.Empty;
        public string? CreationDate { get; set; }
        public string? CreationTime { get; set; }
        public bool IsRead { get; set; }
        public int? PeerMemberID { get; set; }   // ✅ طرف مقابل گفتگو (مربی) — null یعنی پیام سیستمی/مدیریت
        public string? PeerName { get; set; }    // ✅ نام مربی
    }

    public class SendMessageRequest
    {
        public string? Title { get; set; }
        public string Body { get; set; } = string.Empty;
        public byte TargetType { get; set; } = 1;
        public int? TargetRoleID { get; set; }
        public int? TargetSportCatID { get; set; }
        public int? TargetMemberID { get; set; }
    }

    public class ReplyMessageRequest
    {
        public long ReplyToMessageID { get; set; }
        public string? Title { get; set; }
        public string Body { get; set; } = string.Empty;
    }
    /// <summary>گیرندهٔ انتخابی (برای لیست‌های نقش/رشته در مودال ارسال پیام)</summary>
    public class RecipientMemberDto
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = "";
        public string? Mobile { get; set; }
        public string MemberCode { get; set; } = "";
    }

    /// <summary>صفحه‌ای از گیرندگان (برای لیست‌های بلند نقش/رشته با اسکرول مرحله‌ای)</summary>
    public class RecipientPageDto
    {
        public List<RecipientMemberDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Skip { get; set; }
        public bool HasMore { get; set; }
    }

    /// <summary>ارسال پیام به چند عضو مشخص (تیک‌خورده‌ها)</summary>
    public class SendToManyRequest
    {
        public string? Title { get; set; }
        public string Body { get; set; } = string.Empty;
        public List<int> MemberIDs { get; set; } = new();
    }

    public class RoleOptionDto
    {
        public int RoleID { get; set; }
        public string RoleDesc { get; set; } = string.Empty;
    }

    public class SportOptionDto
    {
        public int SportCatID { get; set; }
        public string SportName { get; set; } = string.Empty;
    }

    public class MemberOptionDto
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MemberCode { get; set; } = string.Empty;
    }
}
