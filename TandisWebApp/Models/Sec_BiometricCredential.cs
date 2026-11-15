using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// اعتبارنامهٔ بیومتریک (WebAuthn) برای اثر انگشت / چهرهٔ دستگاه.
    /// هر رکورد = یک دستگاه ثبت‌شده برای یک کاربر.
    /// UserType: member | coach | admin | desk
    /// </summary>
    [Table("Sec_BiometricCredential")]
    public class Sec_BiometricCredential
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CredentialID { get; set; }

        [MaxLength(20)]
        public string UserType { get; set; } = "";

        public int UserID { get; set; }

        [MaxLength(150)]
        public string? UserName { get; set; }

        /// <summary>شناسهٔ اعتبارنامه (base64url) که مرورگر تولید می‌کند</summary>
        [MaxLength(600)]
        public string WebAuthnId { get; set; } = "";

        /// <summary>کلید عمومی به فرمت SPKI (base64) — خروجی getPublicKey() مرورگر</summary>
        public string PublicKeySpki { get; set; } = "";

        /// <summary>الگوریتم امضا: -7 = ES256 ، -257 = RS256</summary>
        public int Alg { get; set; } = -7;

        public long SignCount { get; set; }

        [MaxLength(200)]
        public string? DeviceLabel { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastUsedAt { get; set; }
    }
}
