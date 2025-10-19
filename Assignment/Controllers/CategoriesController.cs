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
            if (!User.HasAnyPermission("GetCategoryAll", "GetCategory"))
            {
                return Forbid();
            }

            return View();
        }
    }
}
