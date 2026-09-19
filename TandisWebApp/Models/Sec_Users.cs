using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    [Table("Sec_Users")]
    public class Sec_User
    {
        [Key]
        public int UserID { get; set; }
        public int? PersonID { get; set; }
        public string? UserName { get; set; }
        public string? UPassword { get; set; }
        public bool? IsAdmin { get; set; }
        public short? ShiftID { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreationDate { get; set; }
    }
}