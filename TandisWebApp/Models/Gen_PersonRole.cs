using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    [Table("Gen_PersonRole")]
    public class Gen_PersonRole
    {
        [Key]
        public int RoleID { get; set; }

        public string? RoleDesc { get; set; }
    }
}