using Microsoft.AspNetCore.Mvc;

namespace DiaryProject.Controllers
{
    public class FrontPageController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Profile()
        {
            return View();
        }
    }

}