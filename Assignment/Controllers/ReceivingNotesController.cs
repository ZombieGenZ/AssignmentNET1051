using Assignment.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers
{
    [Authorize]
    public class ReceivingNotesController : Controller
    {
        public IActionResult Index()
        {
            if (!User.HasPermission("GetReceivingAll") && !User.HasPermission("CreateReceiving"))
            {
                return Forbid();
            }

            ViewData["CanView"] = User.HasPermission("GetReceivingAll");
            ViewData["CanCreate"] = User.HasPermission("CreateReceiving");
            return View();
        }
    }
}
