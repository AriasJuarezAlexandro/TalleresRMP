using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TalleresRMP.Models;
using TalleresRMP.Services;

namespace TalleresRMP.Controllers
{
    public class HomeController : Controller
    {
        private readonly MantenimientoCacheService _cacheService;

        public HomeController(MantenimientoCacheService cacheService)
        {
            _cacheService = cacheService;
        }

        // GET /
        public async Task<IActionResult> Index()
        {
            var mantenimientos = await _cacheService.GetCachedAsync();
            return View(mantenimientos);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
