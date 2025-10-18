using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QCGO.Models; // ✅ This includes Spot, Coordinates, Accessibility, MapOpenHours, SpotInputViewModel

namespace QCGO.Controllers
{
    public class HomeController : Controller
    {
        private readonly QCGO.Services.SpotService _spotService;

        public HomeController(QCGO.Services.SpotService spotService)
        {
            _spotService = spotService;
        }

        // Support multiple tag/district query values (e.g. ?tag=Art&tag=Nature)
        public IActionResult Index(string? q, string[]? tag, string[]? district)
        {
            List<Spot> spots;
            if (!string.IsNullOrWhiteSpace(q) || (tag != null && tag.Length > 0) || (district != null && district.Length > 0))
            {
                spots = _spotService.Search(q, tag, district);
            }
            else
            {
                spots = _spotService.GetAll();
            }

            ViewData["SearchQuery"] = q ?? string.Empty;
            // For views that expect a single string, join multiple values with commas so the UI can display them.
            ViewData["tag"] = tag != null ? string.Join(",", tag) : string.Empty;
            ViewData["district"] = district != null ? string.Join(",", district) : string.Empty;
            // Provide a complete, stable list of categories for the UI
            ViewBag.TopTags = _spotService.GetTopTags(7);
            ViewBag.AllTags = _spotService.GetAllTags();

            Debug.WriteLine($"[DEBUG] Spots fetched from DB: {spots.Count}");

            return View(spots);
        }

        public IActionResult Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var spot = _spotService.GetById(id);
            if (spot == null) return NotFound();
            return View(spot);
        }

        [HttpGet]
        public IActionResult AddSpot()
        {
            // AddSpot functionality has been removed. Redirect to home.
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult AddSpot(SpotInputViewModel model)
        {
            // AddSpot POST disabled. Redirect to home.
            return RedirectToAction("Index");
        }
    }
}
