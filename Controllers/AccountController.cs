using Microsoft.AspNetCore.Mvc;
using ABC_Retail.Services;
using ABC_Retail.Models;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ABC_Retail.Controllers
{
    public class AccountController : Controller
    {
        private readonly AzureStorageService _storageService;
        private readonly PasswordHasher<string> _passwordHasher;

        public AccountController(AzureStorageService storageService)
        {
            _storageService = storageService;
            _passwordHasher = new PasswordHasher<string>();
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Initial Seed: Create default admin if table is empty/missing
            var existingUser = await _storageService.GetAdminUserAsync("admin");
            if (existingUser == null)
            {
                var newUser = new AdminUser
                {
                    RowKey = "admin",
                    FullName = "System Administrator",
                    PasswordHash = _passwordHasher.HashPassword("admin", "password")
                };
                await _storageService.AddAdminUserAsync(newUser);
                existingUser = newUser;
            }

            // Real authentication check
            var user = await _storageService.GetAdminUserAsync(username);
            if (user != null)
            {
                var result = _passwordHasher.VerifyHashedPassword(username, user.PasswordHash, password);
                if (result == PasswordVerificationResult.Success)
                {
                    HttpContext.Session.SetString("UserName", user.FullName ?? username);
                    return RedirectToAction("Index", "Dashboard");
                }
            }

            ViewBag.Error = "Invalid credentials";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
