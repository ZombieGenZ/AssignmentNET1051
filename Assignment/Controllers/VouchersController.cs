using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class VouchersController : Controller
    {
        public IActionResult Index()
        {
            var canViewAll = User.HasPermission("GetVoucherAll");
            var canViewOwn = User.HasPermission("GetVoucher");

            if (!canViewAll && !canViewOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasAnyPermission("CreateVoucher", "CreateVoucherAll");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateVoucher", "UpdateVoucherAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteVoucher", "DeleteVoucherAll");
            ViewData["CanDownloadTemplates"] = User.HasAnyPermission(
                "CreateVoucher",
                "CreateVoucherAll",
                "UpdateVoucher",
                "UpdateVoucherAll");
            ViewData["CanView"] = canViewAll || canViewOwn;

            return View();
        }
    }
}
