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

        public IActionResult Index(string? q, string? tag, string? district)
        {
            List<Spot> spots;
            if (!string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(tag) || !string.IsNullOrWhiteSpace(district))
            {
                spots = _spotService.Search(q, tag, district);
            }
            else
            {
                spots = _spotService.GetAll();
            }

            ViewData["SearchQuery"] = q ?? string.Empty;
            ViewData["tag"] = tag ?? string.Empty;
            ViewData["district"] = district ?? string.Empty;
            ViewBag.TopTags = _spotService.GetTopTags(7);

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
            return View(new SpotInputViewModel());
        }

        [HttpPost]
        public IActionResult AddSpot(SpotInputViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var spot = new Spot
            {
                Name = model.Name,
                District = model.District,
                Barangay = model.Barangay,
                Type = model.Type,
                Description = model.Description,
                Coordinates = new Coordinates
                {
                    Lat = model.Latitude,
                    Lng = model.Longitude
                },
                Tags = model.Tags ?? new List<string>(),
                Accessibility = new Accessibility
                {
                    PublicTransport = model.PublicTransport,
                    ParkingAvailable = model.ParkingAvailable,
                    WheelchairAccessible = model.WheelchairAccessible
                },
                MapOpenHours = new MapOpenHours
                {
                    Url = model.MapUrl
                },
                Rating = 0,
                AddedBy = "admin",
                CreatedAt = DateTime.UtcNow
            };

            _spotService.AddSpot(spot);
            return RedirectToAction("Index");
        }
    }
}
