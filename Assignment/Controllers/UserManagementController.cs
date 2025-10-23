using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assignment.Services.Identity;
using Assignment.ViewModels.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize(Policy = "SuperAdminOnly")]
    [Route("[controller]")]
    public class UserManagementController : Controller
    {
        private readonly IUserManagementService _userManagementService;

        public UserManagementController(
            IUserManagementService userManagementService)
        {
            _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? keyword, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var users = await _userManagementService.GetUsersAsync(keyword, status, page, pageSize);
            return Json(new { success = true, data = users });
        }

        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManagementService.GetUserAsync(id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "Người dùng không tồn tại." });
            }

            return Json(new { success = true, data = user });
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _userManagementService.GetAssignableRolesAsync();
            return Json(new { success = true, data = roles });
        }

        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissions()
        {
            var permissions = await _userManagementService.GetPermissionDefinitionsAsync();
            return Json(new { success = true, data = permissions });
        }

        [HttpPost("users/{id}/roles")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRoles(string id, [FromBody] IEnumerable<string> roles)
        {
            var result = await _userManagementService.UpdateUserRolesAsync(id, roles);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage ?? "Không thể cập nhật vai trò." });
            }

            return Json(new { success = true, message = "Cập nhật vai trò thành công." });
        }

        [HttpPost("users/{id}/permissions")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePermissions(string id, [FromBody] IEnumerable<string> permissions)
        {
            var result = await _userManagementService.UpdateUserPermissionsAsync(id, permissions);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage ?? "Không thể cập nhật quyền." });
            }

            return Json(new { success = true, message = "Cập nhật quyền thành công." });
        }

        [HttpPost("users/{id}/lock")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLockout(string id, [FromBody] LockUserViewModel model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            model.UserId = id;
            var result = await _userManagementService.UpdateUserLockoutAsync(model);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage ?? "Không thể cập nhật trạng thái khóa." });
            }

            var message = model.Unlock
                ? "Đã mở khóa người dùng."
                : model.IsPermanent
                    ? "Đã khóa tài khoản vĩnh viễn."
                    : "Đã cập nhật thời gian khóa tài khoản.";

            return Json(new { success = true, message });
        }

        [HttpPost("users/{id}/settings")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(string id, [FromBody] UpdateUserSettingsRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var result = await _userManagementService.UpdateUserSettingsAsync(id, request.ExcludeFromLeaderboard, request.Booster ?? 1m);
            if (!result.Success)
            {
                return BadRequest(new { success = false, message = result.ErrorMessage ?? "Không thể cập nhật thiết lập người dùng." });
            }

            return Json(new { success = true, message = "Đã cập nhật thiết lập người dùng." });
        }

        [HttpPost("users/bulk-update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUserUpdateRequest request)
        {
            if (request == null || request.UserIds == null || request.UserIds.Count == 0)
            {
                return BadRequest(new { success = false, message = "Vui lòng chọn ít nhất một người dùng." });
            }

            var result = await _userManagementService.BulkUpdateUsersAsync(request);
            var message = $"Đã cập nhật {result.Updated} / {result.TotalSelected} người dùng.";

            return Json(new
            {
                success = true,
                message,
                skipped = result.Skipped,
                errors = result.HasErrors ? result.Errors.ToArray() : Array.Empty<string>()
            });
        }

        public class UpdateUserSettingsRequest
        {
            public bool ExcludeFromLeaderboard { get; set; }

            public decimal? Booster { get; set; }
        }
    }
}
