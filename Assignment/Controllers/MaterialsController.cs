using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class MaterialsController : Controller
    {
        public IActionResult Index()
        {
            var canGetAll = User.HasPermission("GetMaterialAll");
            var canGetOwn = User.HasPermission("GetMaterial");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateMaterial");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateMaterial", "UpdateMaterialAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteMaterial", "DeleteMaterialAll");
            ViewData["CanView"] = canGetAll || canGetOwn;

            return View();
        }
    }
}
