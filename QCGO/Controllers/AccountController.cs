using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using QCGO.Models;
using QCGO.Services;
using Microsoft.Extensions.Logging;

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
                // create claims and sign in
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usernameInput),
                    new Claim("username", usernameInput)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                TempData["AuthMessage"] = "Logged in successfully.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
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
            var created = _accountService.CreateAccount(email, model.Password);
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