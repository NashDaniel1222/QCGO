using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using QCGO.Models;
using QCGO.Services;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace QCGO.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountService _accountService;
        private readonly SpotService _spotService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AccountService accountService, SpotService spotService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _spotService = spotService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model, string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Use AccountService to validate credentials (username/password stored in accounts collection)
            var usernameInput = model.Email?.Trim() ?? string.Empty;
            if (_accountService.ValidateCredentials(usernameInput, model.Password))
            {
                // fetch account to include role/display name
                var acct = _accountService.FindByUsername(usernameInput);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usernameInput),
                    new Claim("username", usernameInput)
                };

                if (acct != null)
                {
                    // include display name and role as claims for easier access
                    if (!string.IsNullOrEmpty(acct.DisplayName))
                        claims.Add(new Claim("displayName", acct.DisplayName));
                    if (!string.IsNullOrEmpty(acct.Role))
                        claims.Add(new Claim(ClaimTypes.Role, acct.Role));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                TempData["AuthMessage"] = "Logged in successfully.";

                // redirect admin users to admin dashboard
                if (acct != null && !string.IsNullOrEmpty(acct.Role) && acct.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Admin", "Account");
                }

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("LandingPage", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("LandingPage", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["AuthMessage"] = "Invalid registration data.";
                return RedirectToAction("Login");
            }

            var email = model.Email.Trim();
            if (_accountService.Exists(email))
            {
                TempData["AuthMessage"] = "An account with that email already exists.";
                return RedirectToAction("Login");
            }

            // NOTE: storing plaintext (match your DB screenshot). Replace with hashing in production.
            var created = _accountService.CreateAccount(email, model.Password, model.Username, "user", model.Birthday, model.Gender);
            if (created)
            {
                TempData["AuthMessage"] = "Registration successful — please sign in.";
            }
            else
            {
                TempData["AuthMessage"] = "Registration failed. Please try again later.";
            }

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Profile()
        {
            // ensure user is authenticated
            if (!User?.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            // read username from claims
            var nameClaim = User?.FindFirst(ClaimTypes.Name)?.Value;
            var userClaim = User?.FindFirst("username")?.Value;
            var username = nameClaim ?? userClaim;
            if (string.IsNullOrEmpty(username))
            {
                return Challenge();
            }

            var acct = _accountService.FindByUsername(username);
            if (acct == null)
            {
                // not found — sign out and challenge
                HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Challenge();
            }

            // If this account is an admin, redirect to the Admin dashboard
            if (!string.IsNullOrEmpty(acct.Role) && acct.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Admin");
            }

            // include bookmarked spots for the profile view
            var bookmarks = acct.Bookmarks ?? new List<string>();
            var bookmarkedSpots = new List<QCGO.Models.Spot>();
            foreach (var id in bookmarks)
            {
                var s = _spotService.GetById(id);
                if (s != null) bookmarkedSpots.Add(s);
            }
            ViewBag.BookmarkedSpots = bookmarkedSpots;

            // pass account to view (shows username and password per request)
            return View(acct);
        }

        [HttpGet]
        public IActionResult Admin()
        {
            // ensure user is authenticated
            if (!User?.Identity?.IsAuthenticated ?? true)
            {
                return Challenge();
            }

            var nameClaim = User?.FindFirst(ClaimTypes.Name)?.Value;
            var username = nameClaim ?? User?.FindFirst("username")?.Value;
            if (string.IsNullOrEmpty(username)) return Challenge();

            var acct = _accountService.FindByUsername(username);
            if (acct == null) return Challenge();

            if (!acct.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                // not an admin - sign out and redirect to login
                HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            // Provide available districts, barangays and types to the view so admin can choose from existing ones
            ViewBag.AllDistricts = _spotService.GetAllDistricts();
            ViewBag.AllBarangays = _spotService.GetAllBarangays();
            ViewBag.AllTypes = _spotService.GetAllTypes();
            // Provide a list of existing spots (id + name) for edit functionality
            ViewBag.AllSpots = _spotService.GetAll().Select(s => new { Id = s.Id, Name = s.Name }).ToList();
            // Render admin Add Spot view (admins can add spots here)
            return View();
        }

        // Return spot details as JSON for client-side editing
        [HttpGet]
        public IActionResult GetSpot(string id)
        {
            if (!User?.Identity?.IsAuthenticated ?? true) return Unauthorized();
            var nameClaim = User?.FindFirst(ClaimTypes.Name)?.Value;
            var username = nameClaim ?? User?.FindFirst("username")?.Value;
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var acct = _accountService.FindByUsername(username);
            if (acct == null || !acct.Role.Equals("admin", StringComparison.OrdinalIgnoreCase)) return Forbid();

            var spot = _spotService.GetById(id);
            if (spot == null) return NotFound();
            return Json(spot);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditSpot(SpotInputViewModel model, string spotId)
        {
            // ensure authenticated admin
            if (!User?.Identity?.IsAuthenticated ?? true) return Challenge();

            var nameClaim = User?.FindFirst(ClaimTypes.Name)?.Value;
            var username = nameClaim ?? User?.FindFirst("username")?.Value;
            if (string.IsNullOrEmpty(username)) return Challenge();

            var acct = _accountService.FindByUsername(username);
            if (acct == null || !acct.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                TempData["AdminMessage"] = "Invalid spot data.";
                ViewBag.AllDistricts = _spotService.GetAllDistricts();
                ViewBag.AllBarangays = _spotService.GetAllBarangays();
                ViewBag.AllTypes = _spotService.GetAllTypes();
                ViewBag.AllSpots = _spotService.GetAll().Select(s => new { Id = s.Id, Name = s.Name }).ToList();
                return View("Admin");
            }

            // Ensure tags field (form may send comma-separated string)
            var tagsField = Request.Form["Tags"].ToString();
            if ((model.Tags == null || model.Tags.Count == 0) && !string.IsNullOrWhiteSpace(tagsField))
            {
                model.Tags = tagsField.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
            }

            var existing = _spotService.GetById(spotId);
            if (existing == null)
            {
                TempData["AdminMessage"] = "Spot not found.";
                ViewBag.AllDistricts = _spotService.GetAllDistricts();
                ViewBag.AllBarangays = _spotService.GetAllBarangays();
                ViewBag.AllTypes = _spotService.GetAllTypes();
                ViewBag.AllSpots = _spotService.GetAll().Select(s => new { Id = s.Id, Name = s.Name }).ToList();
                return View("Admin");
            }

            // Map input to existing spot
            existing.Name = model.Name;
            existing.District = model.District;
            existing.Barangay = model.Barangay;
            existing.Type = model.Type;
            existing.Description = model.Description;
            existing.Tags = model.Tags ?? new List<string>();
            existing.Coordinates = new Coordinates { Lat = model.Latitude, Lng = model.Longitude };
            existing.Accessibility = new Accessibility {
                PublicTransport = model.PublicTransport,
                ParkingAvailable = model.ParkingAvailable,
                WheelchairAccessible = model.WheelchairAccessible
            };
            existing.MapOpenHours = new MapOpenHours { Url = model.MapUrl };

            var updated = _spotService.UpdateSpot(existing);
            if (updated)
            {
                TempData["AdminMessage"] = "Spot updated successfully.";
                return RedirectToAction("Details", "Home", new { id = existing.Id });
            }

            TempData["AdminMessage"] = "Failed to update spot.";
            ViewBag.AllDistricts = _spotService.GetAllDistricts();
            ViewBag.AllBarangays = _spotService.GetAllBarangays();
            ViewBag.AllTypes = _spotService.GetAllTypes();
            ViewBag.AllSpots = _spotService.GetAll().Select(s => new { Id = s.Id, Name = s.Name }).ToList();
            return View("Admin");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Admin(SpotInputViewModel model)
        {
            // ensure authenticated admin
            if (!User?.Identity?.IsAuthenticated ?? true) return Challenge();

            var nameClaim = User?.FindFirst(ClaimTypes.Name)?.Value;
            var username = nameClaim ?? User?.FindFirst("username")?.Value;
            if (string.IsNullOrEmpty(username)) return Challenge();

            var acct = _accountService.FindByUsername(username);
            if (acct == null || !acct.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                TempData["AdminMessage"] = "Invalid spot data.";
                ViewBag.AllDistricts = _spotService.GetAllDistricts();
                ViewBag.AllBarangays = _spotService.GetAllBarangays();
                ViewBag.AllTypes = _spotService.GetAllTypes();
                return View();
            }

            // Ensure tags field (form may send comma-separated string)
            var tagsField = Request.Form["Tags"].ToString();
            if ((model.Tags == null || model.Tags.Count == 0) && !string.IsNullOrWhiteSpace(tagsField))
            {
                model.Tags = tagsField.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
            }

            // Map input model to Spot
            var spot = new Spot
            {
                Name = model.Name,
                District = model.District,
                Barangay = model.Barangay,
                Type = model.Type,
                Description = model.Description,
                Tags = model.Tags ?? new List<string>(),
                Coordinates = new Coordinates { Lat = model.Latitude, Lng = model.Longitude },
                Accessibility = new Accessibility {
                    PublicTransport = model.PublicTransport,
                    ParkingAvailable = model.ParkingAvailable,
                    WheelchairAccessible = model.WheelchairAccessible
                },
                MapOpenHours = new MapOpenHours { Url = model.MapUrl },
                AddedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            var added = _spotService.AddSpot(spot);
            if (added)
            {
                TempData["AdminMessage"] = "Spot added successfully.";
                if (!string.IsNullOrEmpty(spot.Id))
                {
                    return RedirectToAction("Details", "Home", new { id = spot.Id });
                }
                return RedirectToAction("Index", "Home");
            }

            TempData["AdminMessage"] = "Failed to add spot.";
            ViewBag.AllDistricts = _spotService.GetAllDistricts();
            ViewBag.AllBarangays = _spotService.GetAllBarangays();
            ViewBag.AllTypes = _spotService.GetAllTypes();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleBookmark(string spotId, string? returnUrl)
        {
            try
            {
                // Check if this is an AJAX request
                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

                if (!User?.Identity?.IsAuthenticated ?? true)
                {
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = "Please log in to bookmark spots" });
                    }
                    return Challenge();
                }

                var username = User?.FindFirst(ClaimTypes.Name)?.Value ?? User?.FindFirst("username")?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    if (isAjaxRequest)
                    {
                        return Json(new { success = false, message = "User not found" });
                    }
                    return Challenge();
                }

                var bookmarks = _accountService.GetBookmarks(username);
                bool isBookmarked;
                string message;

                if (bookmarks.Contains(spotId))
                {
                    _accountService.RemoveBookmark(username, spotId);
                    isBookmarked = false;
                    message = "Bookmark removed successfully";
                }
                else
                {
                    _accountService.AddBookmark(username, spotId);
                    isBookmarked = true;
                    message = "Spot bookmarked successfully";
                }

                // Return JSON for AJAX requests
                if (isAjaxRequest)
                {
                    return Json(new { 
                        success = true, 
                        isBookmarked = isBookmarked,
                        message = message
                    });
                }

                // Return redirect for regular form submissions
                TempData["BookmarkMessage"] = message;
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error toggling bookmark for spot {SpotId} by user {User}", spotId, User?.Identity?.Name);

                bool isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
                
                if (isAjaxRequest)
                {
                    return Json(new { 
                        success = false, 
                        message = "An error occurred while updating bookmark" 
                    });
                }
                
                TempData["BookmarkMessage"] = "An error occurred while updating bookmark";
                return RedirectToAction("Profile");
            }
        }

        // New method to check bookmark status for a specific spot
        [HttpGet]
        public IActionResult IsBookmarked(string spotId)
        {
            if (!User?.Identity?.IsAuthenticated ?? true)
            {
                return Json(new { isBookmarked = false });
            }

            var username = User?.FindFirst(ClaimTypes.Name)?.Value ?? User?.FindFirst("username")?.Value;
            if (string.IsNullOrEmpty(username))
            {
                return Json(new { isBookmarked = false });
            }

            var bookmarks = _accountService.GetBookmarks(username);
            var isBookmarked = bookmarks.Contains(spotId);

            return Json(new { isBookmarked = isBookmarked });
        }
    }
}