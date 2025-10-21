#nullable disable

using Assignment.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Assignment.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }
        public string Email { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        [BindProperty]
        public PasswordInputModel PasswordInput { get; set; }

        public class InputModel
        {
            [Display(Name = "Họ và tên")]
            [StringLength(256, ErrorMessage = "{0} phải có độ dài từ {2} đến {1} ký tự.", MinimumLength = 2)]
            public string FullName { get; set; }

            [DataType(DataType.Date)]
            [Display(Name = "Ngày sinh")]
            public DateTime? DateOfBirth { get; set; }

            [Phone]
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; }
        }

        public class PasswordInputModel
        {
            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu hiện tại")]
            public string OldPassword { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "{0} phải có độ dài tối thiểu {2} và tối đa {1} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu mới")]
            public string NewPassword { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu mới")]
            [Compare("NewPassword", ErrorMessage = "Mật khẩu mới và mật khẩu xác nhận không giống nhau.")]
            public string ConfirmPassword { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            var email = await _userManager.GetEmailAsync(user);

            Username = userName;
            Email = email;

            Input = new InputModel
            {
                FullName = user.FullName,
                DateOfBirth = user.DateOfBirth,
                PhoneNumber = phoneNumber
            };

            PasswordInput = new PasswordInputModel();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostProfileAsync()
        {
            // Ignore change-password validation rules when only the profile form is submitted.
            // Without removing the password entries, ModelState stays invalid because of the
            // [Required] attributes on PasswordInput, preventing the profile update from running.
            ModelState.Remove(nameof(PasswordInput));
            ModelState.Remove($"{nameof(PasswordInput)}.{nameof(PasswordInputModel.OldPassword)}");
            ModelState.Remove($"{nameof(PasswordInput)}.{nameof(PasswordInputModel.NewPassword)}");
            ModelState.Remove($"{nameof(PasswordInput)}.{nameof(PasswordInputModel.ConfirmPassword)}");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Đã xảy ra lỗi khi cập nhật số điện thoại.";
                    return RedirectToPage();
                }
            }

            var trimmedFullName = string.IsNullOrWhiteSpace(Input.FullName)
                ? null
                : Input.FullName.Trim();

            var hasUserInfoChanged = false;

            if (!string.Equals(user.FullName, trimmedFullName, StringComparison.Ordinal))
            {
                user.FullName = trimmedFullName;
                hasUserInfoChanged = true;
            }

            if (user.DateOfBirth != Input.DateOfBirth)
            {
                user.DateOfBirth = Input.DateOfBirth;
                hasUserInfoChanged = true;
            }

            if (hasUserInfoChanged)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    await LoadAsync(user);
                    return Page();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Thông tin của bạn đã được cập nhật";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            ModelState.Remove("Input.FullName");
            ModelState.Remove("Input.DateOfBirth");
            ModelState.Remove("Input.PhoneNumber");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, PasswordInput.OldPassword, PasswordInput.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                await LoadAsync(user);
                return Page();
            }

            await _userManager.UpdateSecurityStampAsync(user);
            StatusMessage = "Mật khẩu của bạn đã được thay đổi. Vui lòng đăng nhập lại.";

            await _signInManager.SignOutAsync();

            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }
    }
}
