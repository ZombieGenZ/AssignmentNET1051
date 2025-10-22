using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        public IActionResult Index()
        {
            var canView = User.HasAnyPermission("ViewCustomerAll", "ViewTopUserAll");
            if (!canView)
            {
                return Forbid();
            }

            ViewData["CanViewCustomers"] = User.HasPermission("ViewCustomerAll");
            ViewData["CanViewTop"] = User.HasPermission("ViewTopUserAll");
            ViewData["CanManageLeaderboard"] = User.HasPermission("ViewTopUserAll");
            return View();
        }
    }
}
