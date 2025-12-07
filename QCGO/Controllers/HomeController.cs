using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using QCGO.Models; // ✅ This includes Spot, Coordinates, Accessibility, MapOpenHours, SpotInputViewModel

namespace QCGO.Controllers
{
    public class HomeController : Controller
    {
        private readonly QCGO.Services.SpotService _spotService;
        private readonly QCGO.Services.AccountService _accountService;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public HomeController(QCGO.Services.SpotService spotService, QCGO.Services.AccountService accountService, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _spotService = spotService;
            _accountService = accountService;
            _env = env;
        }

        // Support multiple tag/district query values (e.g. ?tag=Art&tag=Nature)
        public IActionResult Index(string? q, string[]? tag, string[]? district, string? group)
        {
            List<Spot> spots;
            if (!string.IsNullOrWhiteSpace(group))
            {
                // Map group to member tags then search by those tags; do not treat member tags as UI-selected tags here.
                var (groupsMap, _) = BuildCategoryGroups();
                var memberTags = groupsMap.ContainsKey(group) ? groupsMap[group] : new List<string>();
                spots = _spotService.Search(q, memberTags, district);
            }
            else if (!string.IsNullOrWhiteSpace(q) || (tag != null && tag.Length > 0) || (district != null && district.Length > 0))
            {
                spots = _spotService.Search(q, tag, district);
            }
            else
            {
                spots = _spotService.GetAll();
            }

            // Populate sidebar / view data consistently via helper
            PopulateSidebarData(q, tag, district, group);

            Debug.WriteLine($"[DEBUG] Spots fetched from DB: {spots.Count}");

            return View(spots);
        }

        public IActionResult Details(string id, string? q, string[]? tag, string[]? district)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var spot = _spotService.GetById(id);
            if (spot == null) return NotFound();
            // Ensure sidebar data is populated so the layout renders consistently
            PopulateSidebarData(q, tag, district, null);
            // Determine whether current user has this spot bookmarked
            var isBookmarked = false;
            if (User?.Identity?.IsAuthenticated == true)
            {
                var username = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? User?.FindFirst("username")?.Value;
                if (!string.IsNullOrEmpty(username))
                {
                    var bookmarks = _accountService.GetBookmarks(username);
                    isBookmarked = bookmarks.Contains(id);
                }
            }
            ViewBag.IsBookmarked = isBookmarked;

            var imageUrl = spot.GetImageUrl();

            return View(spot);
        }

        // New action: QC Map view that shows the Leaflet map for all (or filtered) spots
        public IActionResult QCMap(string? q, string[]? tag, string[]? district, string? group)
        {
            List<Spot> spots;
            if (!string.IsNullOrWhiteSpace(group))
            {
                var (groupsMap, _) = BuildCategoryGroups();
                var memberTags = groupsMap.ContainsKey(group) ? groupsMap[group] : new List<string>();
                spots = _spotService.Search(q, memberTags, district);
            }
            else if (!string.IsNullOrWhiteSpace(q) || (tag != null && tag.Length > 0) || (district != null && district.Length > 0))
            {
                spots = _spotService.Search(q, tag, district);
            }
            else
            {
                spots = _spotService.GetAll();
            }

            PopulateSidebarData(q, tag, district, group);
            return View(spots);
        }

        // Suggest endpoint: return lightweight JSON suggestions for autosuggest in the layout
        [HttpGet]
        public IActionResult Suggest(string? q, string[]? tag, string[]? district, int limit = 8)
        {
            if (string.IsNullOrWhiteSpace(q)) return Json(new object[0]);

            // Use existing search functionality; SpotService.Search already performs case-insensitive regex matching
            var results = _spotService.Search(q, tag, district)
                                      .Where(s => s != null)
                                      .Select(s => new {
                                          id = s.Id,
                                          name = s.Name ?? string.Empty,
                                          barangay = s.Barangay ?? string.Empty,
                                          district = s.District ?? string.Empty
                                      })
                                      .Take(limit)
                                      .ToList();

            return Json(results);
        }

        // New action: District dashboard (populates district image paths)
        public IActionResult DistrictDashboard()
        {
            ViewBag.Districts = BuildDistrictImageList();
            return View();
        }

        // Landing page - default route target
        public IActionResult LandingPage()
        {
            // Populate shared sidebar data so the layout renders consistently
            PopulateSidebarData(null, null, null, null);
            // Provide district images for the landing page carousel
            ViewBag.Districts = BuildDistrictImageList();
            return View();
        }

        // About page describing the system and the team
        public IActionResult About()
        {
            // Keep sidebar/layout consistent
            PopulateSidebarData(null, null, null, null);
            ViewBag.Districts = BuildDistrictImageList();
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
            PopulateSidebarData(null, null, string.IsNullOrEmpty(district) ? null : new[] { district }, null);

            ViewData["SelectedDistrict"] = district ?? string.Empty;
            ViewData["SelectedCategory"] = category ?? string.Empty;

            return View(filtered);
        }

        // AddSpot functionality removed intentionally.

        // Helper to populate sidebar data that multiple actions and the layout expect.
        // Overload: include selected group
        private void PopulateSidebarData(string? q, string[]? tag, string[]? district, string? group)
        {
            ViewData["SearchQuery"] = q ?? string.Empty;
            // For views that expect a single string, join multiple values with commas so the UI can display them.
            ViewData["tag"] = tag != null ? string.Join(",", tag) : string.Empty;
            ViewData["district"] = district != null ? string.Join(",", district) : string.Empty;
            // Provide a complete, stable list of categories for the UI
            ViewBag.TopTags = _spotService.GetTopTags(7);
            ViewBag.AllTags = _spotService.GetAllTags();
            // Expose counts so the layout can display accurate numbers
            ViewBag.TagCounts = _spotService.GetTagCounts();
            ViewBag.DistrictCounts = _spotService.GetDistrictCounts();
            ViewBag.AllDistricts = _spotService.GetAllDistricts();
            // Mark the selected group (if any) so the layout can highlight the grouped category instead of individual tags
            ViewBag.SelectedGroup = group ?? string.Empty;
            
            // Build grouped categories (map similar tags into a group).
            try
            {
                var tagCounts = ViewBag.TagCounts as Dictionary<string, int> ?? new Dictionary<string, int>();
                var allTags = ViewBag.AllTags as List<string> ?? new List<string>();

                // Define grouping keywords (lowercase) - first match wins
                var groupKeywordMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Dining & Food", new[] { "dining", "restaurant", "food", "eat", "eatery", "diner", "dining" } },
                    { "Cafe & Coffee", new[] { "cafe", "coffee", "bakery" } },
                    { "Shopping", new[] { "mall", "shopping", "boutique", "market" } },
                    { "Sightseeing & Parks", new[] { "sightseeing", "view", "park", "museum", "gallery", "trail", "nature", "landmark" } },
                    { "Art & Culture", new[] { "art", "gallery", "cultural", "museum", "exhibit" } },
                    { "Outdoors & Nature", new[] { "nature", "beach", "ocean", "trail", "hike", "park" } },
                    { "Wellness", new[] { "wellness", "spa", "gym", "fitness" } },
                    { "Entertainment", new[] { "entertainment", "theater", "movie", "music", "concert" } },
                    { "Family & Kids", new[] { "kids", "family", "playground", "school" } },
                    { "Nightlife", new[] { "bar", "pub", "nightlife", "club" } }
                };

                var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                // Initialize group keys
                foreach (var g in groupKeywordMap.Keys) groups[g] = new List<string>();
                groups["Other"] = new List<string>();

                foreach (var tagName in allTags)
                {
                    var name = tagName ?? string.Empty;
                    var assigned = false;
                    var lower = name.ToLowerInvariant();
                    foreach (var kv in groupKeywordMap)
                    {
                        foreach (var kw in kv.Value)
                        {
                            if (lower.Contains(kw))
                            {
                                groups[kv.Key].Add(name);
                                assigned = true;
                                break;
                            }
                        }
                        if (assigned) break;
                    }
                    if (!assigned)
                    {
                        groups["Other"].Add(name);
                    }
                }

                // Build list of group objects with counts
                var groupList = groups.Select(g => new
                {
                    Name = g.Key,
                    Tags = g.Value.OrderBy(t => t).ToList(),
                    Count = g.Value.Sum(t => tagCounts.ContainsKey(t) ? tagCounts[t] : 0)
                })
                // Exclude empty groups
                .Where(g => g.Count > 0)
                .OrderByDescending(g => g.Count)
                .Take(12) // show up to 12 grouped categories
                .ToList();

                ViewBag.GroupedCategories = groupList;
                // Also expose member tags for the selected group so the layout can mark individual tags when searched
                if (!string.IsNullOrEmpty(group) && groups.ContainsKey(group))
                {
                    ViewBag.GroupMemberTags = groups[group];
                }
                else
                {
                    ViewBag.GroupMemberTags = new List<string>();
                }
            }
            catch
            {
                ViewBag.GroupedCategories = new List<object>();
            }
        }

        // Helper that returns the groups mapping and a prepared list for the view. Reusable by actions.
        private (Dictionary<string, List<string>> groupsMap, List<object> groupList) BuildCategoryGroups()
        {
            var tagCounts = _spotService.GetTagCounts() ?? new Dictionary<string, int>();
            var allTags = _spotService.GetAllTags() ?? new List<string>();

            var groupKeywordMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Dining & Food", new[] { "dining", "restaurant", "food", "eat", "eatery", "diner", "dining" } },
                    { "Cafe & Coffee", new[] { "cafe", "coffee", "bakery" } },
                    { "Shopping", new[] { "mall", "shopping", "boutique", "market" } },
                    { "Sightseeing & Parks", new[] { "sightseeing", "view", "park", "museum", "gallery", "trail", "nature", "landmark" } },
                    { "Art & Culture", new[] { "art", "gallery", "cultural", "museum", "exhibit" } },
                    { "Outdoors & Nature", new[] { "nature", "beach", "ocean", "trail", "hike", "park" } },
                    { "Wellness", new[] { "wellness", "spa", "gym", "fitness" } },
                    { "Entertainment", new[] { "entertainment", "theater", "movie", "music", "concert" } },
                    { "Family & Kids", new[] { "kids", "family", "playground", "school" } },
                    { "Nightlife", new[] { "bar", "pub", "nightlife", "club" } }
                };

            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in groupKeywordMap.Keys) groups[g] = new List<string>();
            groups["Other"] = new List<string>();

            foreach (var tagName in allTags)
            {
                var name = tagName ?? string.Empty;
                var assigned = false;
                var lower = name.ToLowerInvariant();
                foreach (var kv in groupKeywordMap)
                {
                    foreach (var kw in kv.Value)
                    {
                        if (lower.Contains(kw))
                        {
                            groups[kv.Key].Add(name);
                            assigned = true;
                            break;
                        }
                    }
                    if (assigned) break;
                }
                if (!assigned) groups["Other"].Add(name);
            }

            var groupList = groups.Select(g => new
            {
                Name = g.Key,
                Tags = g.Value.OrderBy(t => t).ToList(),
                Count = g.Value.Sum(t => tagCounts.ContainsKey(t) ? tagCounts[t] : 0)
            })
            .Where(g => g.Count > 0)
            .OrderByDescending(g => g.Count)
            .Take(12)
            .ToList<object>();

            return (groups, groupList);
        }

        // Build a shared district list with image URLs (first image in wwwroot/images/<District Name>/)
        private List<object> BuildDistrictImageList()
        {
            var baseDistricts = new[] {
                new { Id = 1, Name = "District 1", Description = "The Spanish Colonial Core." },
                new { Id = 2, Name = "District 2", Description = "Historic neighborhoods and parks." },
                new { Id = 3, Name = "District 3", Description = "Cultural centers and galleries." },
                new { Id = 4, Name = "District 4", Description = "Markets and local flavors." },
                new { Id = 5, Name = "District 5", Description = "Modern districts with skyline views." },
                new { Id = 6, Name = "District 6", Description = "Quiet residential pockets." }
            };

            var result = new List<object>();
            var webRoot = _env?.WebRootPath ?? string.Empty;
            foreach (var d in baseDistricts)
            {
                var folderName = d.Name;
                var folderPath = System.IO.Path.Combine(webRoot, "images", folderName);
                var imageUrls = new List<string>();
                string fallback = $"/images/district{d.Id}.svg"; // fallback single
                try
                {
                    if (System.IO.Directory.Exists(folderPath))
                    {
                        var files = System.IO.Directory.EnumerateFiles(folderPath)
                                    .Where(f => {
                                        var ext = System.IO.Path.GetExtension(f)?.ToLowerInvariant();
                                        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".jfif" || ext == ".webp" || ext == ".svg";
                                    })
                                    .OrderBy(f => f)
                                    .ToList();

                        foreach (var f in files)
                        {
                            var fileName = System.IO.Path.GetFileName(f);
                            imageUrls.Add($"/images/{folderName}/{fileName}".Replace("\\", "/"));
                        }
                    }
                }
                catch
                {
                    // ignore errors and keep fallback
                }

                if (imageUrls.Count == 0)
                {
                    imageUrls.Add(fallback);
                }

                result.Add(new { Id = d.Id, Name = d.Name, Description = d.Description, ImageUrls = imageUrls });
            }

            return result;
        }
    }
}
