using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class CombosController : Controller
    {
        public IActionResult Index()
        {
            var canGetAll = User.HasPermission("GetComboAll");
            var canGetOwn = User.HasPermission("GetCombo");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateCombo");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateCombo", "UpdateComboAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteCombo", "DeleteComboAll");
            ViewData["CanView"] = canGetAll || canGetOwn;

            return View();
        }
    }
}
