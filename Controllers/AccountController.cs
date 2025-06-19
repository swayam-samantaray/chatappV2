using ChatApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace ChatApp.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginModel model)
        {
            // Dummy login check
            if ((model.Username == "mohit" && model.Password == "1234")|| (model.Username =="admin" && model.Password =="123"))
            {
                HttpContext.Session.SetString("username", model.Username);

                return Ok();
                // return RedirectToAction("Chat");
            }
            else
            {
                ViewBag.Error = "Invalid login!";
                return View();
            }
        }

        public IActionResult Chat()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("username")))
            {
                return Content("<script>alert('Please login first!'); window.location.href='/Account/Login';</script>", "text/html");
            }

            ViewBag.Username = HttpContext.Session.GetString("username");
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}


