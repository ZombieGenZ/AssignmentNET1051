using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class RewardsController : Controller
    {
        public IActionResult Index()
        {
            var canViewAll = User.HasPermission("GetRewardAll");
            var canViewOwn = User.HasPermission("GetReward");

            if (!canViewAll && !canViewOwn)
            {
                return Forbid();
            }

            ViewData["CanCreate"] = User.HasPermission("CreateReward");
            ViewData["CanUpdate"] = User.HasAnyPermission("UpdateReward", "UpdateRewardAll");
            ViewData["CanDelete"] = User.HasAnyPermission("DeleteReward", "DeleteRewardAll");
            ViewData["CanView"] = canViewAll || canViewOwn;

            return View();
        }
    }
}
