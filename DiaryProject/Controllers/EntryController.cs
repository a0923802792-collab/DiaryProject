using Microsoft.AspNetCore.Mvc;

namespace DiaryProject.Controllers
{
    public class EntryController : Controller
    {
        public IActionResult Welcome()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Register()
        {
            return View();
        }
    }
}