using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using QCGO.Models;
using QCGO.Services;


namespace QCGO.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountService _accountService;

        public AccountController(AccountService accountService)
        {
            _accountService = accountService;
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
    }
}
