using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class CategoriesController : Controller
    {
        public IActionResult Index()
        {
            var canGetAll = User.HasPermission("GetCategoryAll");
            var canGetOwn = User.HasPermission("GetCategory");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateCategory");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateCategory", "UpdateCategoryAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteCategory", "DeleteCategoryAll");
            ViewData["CanView"] = canGetAll || canGetOwn;

            return View();
        }
    }
}
