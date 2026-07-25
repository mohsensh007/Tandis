using System.ComponentModel.DataAnnotations;

namespace TandisWebApp.DTOs
{
    // ============================================================
    //  احراز هویت
    // ============================================================

    public class LoginRequest
    {
        [Required(ErrorMessage = "کد ملی را وارد کنید")]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "کد ملی باید ۱۰ رقم باشد")]
        public string NationalCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور را وارد کنید")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public int MemberID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Mobile { get; set; }
        public short ShiftID { get; set; }
        public bool MustChangePassword { get; set; }
        public string ReturnUrl { get; set; } = "/Home";
    }

    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "رمز قدیم را وارد کنید")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز جدید را وارد کنید")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "رمز جدید حداقل ۳ کاراکتر")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تکرار رمز را وارد کنید")]
        [Compare(nameof(NewPassword), ErrorMessage = "رمز جدید و تکرار آن مطابقت ندارد")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // ============================================================
    //  پاس‌های عمومی
    // ============================================================

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class SimpleResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
