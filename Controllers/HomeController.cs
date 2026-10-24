using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TalleresRMP.Filters;
using TalleresRMP.Models;
using TalleresRMP.Services;

namespace TalleresRMP.Controllers
{
    public class HomeController : Controller
    {
        // GET / -- la pantalla de inicio ahora es la lista de mantenimientos.
        [RequiereSesion]
        public IActionResult Index() => RedirectToAction("Index", "Mantenimiento");

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
