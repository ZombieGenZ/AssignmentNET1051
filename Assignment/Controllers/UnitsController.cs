using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class UnitsController : Controller
    {
        public IActionResult Index()
        {
            var canGetAll = User.HasPermission("GetUnitAll");
            var canGetOwn = User.HasPermission("GetUnit");

            if (!canGetAll && !canGetOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateUnit");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateUnit", "UpdateUnitAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteUnit", "DeleteUnitAll");
            ViewData["CanView"] = canGetAll || canGetOwn;

            return View();
        }
    }
}
