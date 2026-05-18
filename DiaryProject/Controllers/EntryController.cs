using Microsoft.AspNetCore.Mvc;

namespace DiaryProject.Controllers
{
    public class EntryController : Controller
    {
        // 預設歡迎頁，回 Welcome.cshtml
        public IActionResult Welcome() => View();

        // 以下 4 個 action 共用 Welcome.cshtml
        // 網址會顯示 /Entry/Login、/Entry/Register 等，但實際載入 Welcome.cshtml
        // entry.js 的 React Router 會根據 URL render 對應的 React 元件
        public IActionResult Login() => View("Welcome");
        public IActionResult Register() => View("Welcome");
        public IActionResult ForgotPassword() => View("Welcome");
        public IActionResult Verify() => View("Welcome");
    }
}