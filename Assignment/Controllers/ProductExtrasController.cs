using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class ProductExtrasController : Controller
    {
        public IActionResult Index()
        {
            var canViewAll = User.HasPermission("GetProductExtraAll");
            var canViewOwn = User.HasPermission("GetProductExtra");

            ViewData["CanCreate"] = User.HasPermission("CreateProductExtra");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateProductExtraAll", "UpdateProductExtra");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteProductExtraAll", "DeleteProductExtra");
            ViewData["CanView"] = canViewAll || canViewOwn;

            return View();
        }
    }
}
