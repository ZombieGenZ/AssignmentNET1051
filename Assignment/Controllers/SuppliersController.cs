using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class SuppliersController : Controller
    {
        public IActionResult Index()
        {
            var canGetAll = User.HasPermission("GetSupplierAll");
            var canGetOwn = User.HasPermission("GetSupplier");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateSupplier");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateSupplier", "UpdateSupplierAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteSupplier", "DeleteSupplierAll");
            ViewData["CanView"] = canGetAll || canGetOwn;

            return View();
        }
    }
}
