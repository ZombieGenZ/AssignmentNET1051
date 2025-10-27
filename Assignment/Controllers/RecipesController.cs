using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class RecipesController : Controller
    {
        public IActionResult Index()
        {
            var canGetAll = User.HasPermission("GetRecipeAll");
            var canGetOwn = User.HasPermission("GetRecipe");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateRecipe");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateRecipe", "UpdateRecipeAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteRecipe", "DeleteRecipeAll");
            ViewData["CanView"] = canGetAll || canGetOwn;

            return View();
        }
    }
}
