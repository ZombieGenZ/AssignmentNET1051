using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Assignment.Services.Identity;
using Assignment.ViewModels.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize(Policy = "SuperAdminOnly")]
    [Route("[controller]")]
    public class RoleManagementController : Controller
    {
        private readonly IRoleManagementService _roleService;

        public RoleManagementController(
            IRoleManagementService roleService)
        {
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _roleService.CreateTemplateAsync();
            return View(model);
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles([FromQuery] string? keyword, [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _roleService.GetRolesAsync(keyword, status, page, pageSize);
            return Json(new { success = true, data = result });
        }

        [HttpGet("roles/{id}")]
        public async Task<IActionResult> GetRole(string id)
        {
            var role = await _roleService.GetRoleAsync(id);
            if (role == null)
            {
                return NotFound(new { success = false, message = "Vai trò không tồn tại." });
            }

            return Json(new { success = true, data = role });
        }

        [HttpPost("roles")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole([FromBody] RoleManagementViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToArray();
                return BadRequest(new { success = false, message = string.Join(" ", errors) });
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { success = false, message = "Tên vai trò không được để trống." });
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _roleService.CreateRoleAsync(model, currentUserId);

            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                return BadRequest(new { success = false, message = errors });
            }

            return Json(new { success = true, message = "Tạo vai trò thành công." });
        }

        [HttpPut("roles/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(string id, [FromBody] RoleManagementViewModel model)
        {
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { success = false, message = "Tên vai trò không được để trống." });
            }

            model.Id = id;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _roleService.UpdateRoleAsync(model, currentUserId);

            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                return BadRequest(new { success = false, message = errors });
            }

            return Json(new { success = true, message = "Cập nhật vai trò thành công." });
        }

        [HttpDelete("roles/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var (success, error) = await _roleService.SoftDeleteRoleAsync(id, currentUserId);

            if (!success)
            {
                return BadRequest(new { success = false, message = error ?? "Không thể xóa vai trò." });
            }

            return Json(new { success = true, message = "Đã xóa vai trò." });
        }

        [HttpGet("templates")]
        public async Task<IActionResult> GetTemplate()
        {
            var template = await _roleService.CreateTemplateAsync();
            return Json(new { success = true, data = template });
        }
    }
}
