using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize(Policy = "ViewInventoryPolicy")]
    public class InventoriesController : Controller
    {
        public IActionResult Index()
        {
            ViewData["CanViewAll"] = User.HasPermission("ViewInventoryAll");
            return View();
        }
    }
}
