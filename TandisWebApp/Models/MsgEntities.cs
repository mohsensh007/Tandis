using System.ComponentModel.DataAnnotations;          // ✅ حتماً اضافه باشه
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    [Table("Msg_Message")]
    public class Msg_Message
    {
        [Key]                                            // ✅ اضافه شد
        public long MessageID { get; set; }
        public string? Title { get; set; }
        public string Body { get; set; } = string.Empty;
        public byte TargetType { get; set; }
        public int? TargetRoleID { get; set; }
        public int? TargetSportCatID { get; set; }
        public int? TargetMemberID { get; set; }
        public short? SenderUserID { get; set; }
        public int? SenderMemberID { get; set; }
        public bool IsSeen { get; set; }
        public bool IsActive { get; set; }
        public string? CreationDate { get; set; }
        public string? CreationTime { get; set; }
        public DateTime CreationDateTime { get; set; }
    }

    [Table("Msg_Read")]
    public class Msg_Read
    {
        [Key]                                            // ✅ اضافه شد
        public long ReadID { get; set; }
        public long MessageID { get; set; }
        public int MemberID { get; set; }
        public DateTime ReadDateTime { get; set; }
    }
}