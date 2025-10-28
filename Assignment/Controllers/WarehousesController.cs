using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class WarehousesController : Controller
    {
        public IActionResult Index()
        {
            var canGetAll = User.HasPermission("GetWarehouseAll");
            var canGetOwn = User.HasPermission("GetWarehouse");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateWarehouse");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateWarehouse", "UpdateWarehouseAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteWarehouse", "DeleteWarehouseAll");
            ViewData["CanView"] = canGetAll || canGetOwn;

            return View();
        }
    }
}
