using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QualityDoc.Models;

namespace QualityDoc.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // La pantalla inicial siempre es el área de Documentos (o el login si no hay sesión).
            if (User?.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Documentos");
            return RedirectToAction("Login", "Auth");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
