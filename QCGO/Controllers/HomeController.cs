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

            // Populate sidebar / view data consistently via helper
            PopulateSidebarData(q, tag, district);

            Debug.WriteLine($"[DEBUG] Spots fetched from DB: {spots.Count}");

            return View(spots);
        }

        public IActionResult Details(string id, string? q, string[]? tag, string[]? district)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var spot = _spotService.GetById(id);
            if (spot == null) return NotFound();
            // Ensure sidebar data is populated so the layout renders consistently
            PopulateSidebarData(q, tag, district);
            return View(spot);
        }

        // New action: QC Map view that shows the Leaflet map for all (or filtered) spots
        public IActionResult QCMap(string? q, string[]? tag, string[]? district)
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

            PopulateSidebarData(q, tag, district);
            return View(spots);
        }

        // New action: District dashboard (placeholder view)
        public IActionResult DistrictDashboard()
        {
            // No logic yet - placeholder view
            return View();
        }

        // Landing page - default route target
        public IActionResult LandingPage()
        {
            // Populate shared sidebar data so the layout renders consistently
            PopulateSidebarData(null, null, null);
            return View();
        }

        // Show suggestions for a specific district and category (category maps to tags)
        public IActionResult DistrictSuggestions(string? district, string? category)
        {
            // Normalize inputs
            district = string.IsNullOrWhiteSpace(district) ? null : district.Trim();
            category = string.IsNullOrWhiteSpace(category) ? "More" : category.Trim();

            // Fetch spots for the district first
            var allInDistrict = new List<Spot>();
            if (!string.IsNullOrEmpty(district))
            {
                allInDistrict = _spotService.Search(null, null, new[] { district });
            }
            else
            {
                allInDistrict = _spotService.GetAll();
            }

            // Define keyword maps for categories (case-insensitive contains)
            var maps = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "Dining", new[] { "dining", "restaurant", "food", "eat", "eatery", "casual" } },
                { "Cafe", new[] { "cafe", "coffee", "coffee shop", "bakery" } },
                { "Malls", new[] { "mall", "malls", "shopping", "shopping mall" } },
                { "Sightseeing", new[] { "sightseeing", "view", "park", "museum", "gallery", "attraction", "landmark", "trail", "nature" } }
            };

            bool MatchesKeywords(string? type, string[] keywords)
            {
                if (string.IsNullOrWhiteSpace(type)) return false;
                var lower = type.ToLowerInvariant();
                foreach (var k in keywords)
                {
                    if (lower.Contains(k)) return true;
                }
                return false;
            }

            List<Spot> filtered;

            if (string.Equals(category, "More", StringComparison.OrdinalIgnoreCase))
            {
                // More = spots that do NOT match any of the known categories
                filtered = allInDistrict.Where(s =>
                    {
                        var t = s?.Type ?? string.Empty;
                        foreach (var kv in maps)
                        {
                            if (MatchesKeywords(t, kv.Value)) return false;
                        }
                        return true;
                    }).ToList();
            }
            else if (maps.ContainsKey(category))
            {
                var keys = maps[category];
                filtered = allInDistrict.Where(s => MatchesKeywords(s?.Type, keys)).ToList();
            }
            else
            {
                // Unknown category: fall back to checking tags (legacy) or return empty
                filtered = allInDistrict.Where(s => s.Tags != null && s.Tags.Contains(category, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            // Keep sidebar data consistent if any (pass category as tag for display but no tag filtering)
            PopulateSidebarData(null, null, string.IsNullOrEmpty(district) ? null : new[] { district });

            ViewData["SelectedDistrict"] = district ?? string.Empty;
            ViewData["SelectedCategory"] = category ?? string.Empty;

            return View(filtered);
        }

        // AddSpot functionality removed intentionally.

        // Helper to populate sidebar data that multiple actions and the layout expect.
        private void PopulateSidebarData(string? q, string[]? tag, string[]? district)
        {
            ViewData["SearchQuery"] = q ?? string.Empty;
            // For views that expect a single string, join multiple values with commas so the UI can display them.
            ViewData["tag"] = tag != null ? string.Join(",", tag) : string.Empty;
            ViewData["district"] = district != null ? string.Join(",", district) : string.Empty;
            // Provide a complete, stable list of categories for the UI
            ViewBag.TopTags = _spotService.GetTopTags(7);
            ViewBag.AllTags = _spotService.GetAllTags();
        }
    }
}
