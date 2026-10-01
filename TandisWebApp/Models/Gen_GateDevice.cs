using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// معادل dbo.Gen_GateDevice - دستگاه‌های گیت (کیوسک / تشخیص چهره / اثر انگشت)
    /// (معادل FullSportDB.dbml در پروژه کیوسک)
    /// </summary>
    [Table("Gen_GateDevice")]
    public class Gen_GateDevice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GateDeviceID { get; set; }

        [MaxLength(255)]
        public string? GateName { get; set; }

        /// <summary>9 = Kiosk (MyProvider.TrafficEnum.Kiosk)</summary>
        public short? TrafficType { get; set; }

        /// <summary>60070 = Tiam (MyProvider.DeviceEnum.Tiam) - دستگاه تشخیص چهره</summary>
        public int? DeviceID { get; set; }

        /// <summary>آدرس IP دستگاه (مثلاً 192.168.1.14)</summary>
        [MaxLength(50)]
        public string? IPAddress { get; set; }

        public short? TerminalNo { get; set; }

        [MaxLength(50)]
        public string? SerialNo { get; set; }

        /// <summary>پورت سرویس دستگاه (Tiam معمولاً 5005)</summary>
        [MaxLength(50)]
        public string? PortNo { get; set; }

        /// <summary>آدرس سروری که دستگاه به آن وصل می‌شود (سرور Traffic روی پورت 8085)</summary>
        [MaxLength(50)]
        public string? ServerIP { get; set; }

        public bool? OpenDoor { get; set; }
    }
}
