using Microsoft.AspNetCore.Mvc;

namespace DiaryProject.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}