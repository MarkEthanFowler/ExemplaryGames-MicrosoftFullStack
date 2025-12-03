using Microsoft.AspNetCore.Mvc;

namespace ExemplaryGames.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Error(int? statusCode = null)
        {
            int code = statusCode ?? 500;

            ViewBag.Status = code;

            switch(code)
            {
                case 404:
                    ViewBag.Message = "The page you are looking for could not be found";
                    break;
                case 403:
                    ViewBag.Message = "The do not have permission to access this page.";
                    break;
                default:
                    ViewBag.Message = "An unexpected error occurred.";
                    break;

            }

            return View();
        }
    }
}
