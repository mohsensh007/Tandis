using System.ComponentModel.DataAnnotations;

namespace TandisWebApp.DTOs
{
    public class AdminInboxRowDto
    {
        public long MessageID { get; set; }
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
}
