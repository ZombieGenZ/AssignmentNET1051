using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        public IActionResult Index()
        {
            var canViewAll = User.HasPermission("GetProductAll");
            var canViewOwn = User.HasPermission("GetProduct");

            if (!canViewAll && !canViewOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateProduct");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateProductAll", "UpdateProduct");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteProductAll", "DeleteProduct");
            ViewData["CanView"] = canViewAll || canViewOwn;

            return View();
        }
    }
}
