using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Net;
using System.Net.Mail;

namespace ProyectoFinal.Controllers
{
    public class LoginController : Controller
    {
        private readonly ApplicationDbContext _context;


        private const string EmailFrom = "andrescaleraa7@gmail.com";
        private const string EmailPassword = "aytu wnvj vyqm pntn";

        public LoginController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Login
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }
            return View(new LoginViewModel());
        }

        // POST: /Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel loginViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(loginViewModel);
            }

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == loginViewModel.UserName && u.Password == loginViewModel.Password);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid username or password.");
                    return View(loginViewModel);
                }

               
                if (user.MustChangePassword)
                {
               
                    HttpContext.Session.SetInt32("ChangePasswordUserId", user.UserId);
                    HttpContext.Session.SetString("ChangePasswordUserName", user.UserName);
                    return RedirectToAction("ChangePassword");
                }

               
                await SignInUser(user);
                return RedirectToDashboard(user.Role);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error logging in. Please try again.");
                return View(loginViewModel);
            }
        }

        // GET: /Login/ChangePassword
        public IActionResult ChangePassword()
        {
            var userId = HttpContext.Session.GetInt32("ChangePasswordUserId");
            var userName = HttpContext.Session.GetString("ChangePasswordUserName");

            if (userId == null || string.IsNullOrEmpty(userName))
            {
                return RedirectToAction("Index");
            }

            ViewBag.UserName = userName;
            return View();
        }

        // POST: /Login/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string newPassword, string confirmPassword)
        {
            var userId = HttpContext.Session.GetInt32("ChangePasswordUserId");
            var userName = HttpContext.Session.GetString("ChangePasswordUserName");

            if (userId == null)
            {
                return RedirectToAction("Index");
            }

            ViewBag.UserName = userName;

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            {
                ModelState.AddModelError(string.Empty, "Password must be at least 8 characters long.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return View();
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return RedirectToAction("Index");
                }

                
                user.Password = newPassword;
                user.MustChangePassword = false;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                HttpContext.Session.Remove("ChangePasswordUserId");
                HttpContext.Session.Remove("ChangePasswordUserName");

                
                await SignInUser(user);
                return RedirectToDashboard(user.Role);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error changing password. Please try again.");
                return View();
            }
        }

        // GET: /Login/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Login/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(User newUser, string confirmPassword)
        {
            if (!ModelState.IsValid)
            {
                return View(newUser);
            }

            if (newUser.Password != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return View(newUser);
            }

            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == newUser.UserName || u.Email == newUser.Email);

                if (existingUser != null)
                {
                    if (existingUser.UserName == newUser.UserName)
                        ModelState.AddModelError(string.Empty, "Username is already in use.");
                    if (existingUser.Email == newUser.Email)
                        ModelState.AddModelError(string.Empty, "Email address is already registered.");
                    return View(newUser);
                }

                newUser.Role = "Client";
                newUser.MustChangePassword = false;
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registration successful. You can now log in.";
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error registering user. Please try again.");
                return View(newUser);
            }
        }

        // GET: /Login/ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Login/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "Please enter your email address.");
                return View();
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user != null)
                {
              
                    var newPassword = GeneratePassword();

                    user.Password = newPassword;
                    user.MustChangePassword = true;  
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

     
                    await SendNewPasswordEmail(email, user.UserName, newPassword);

                    TempData["SuccessMessage"] = "A new password has been sent to your email address.";
                }
                else
                {
                    TempData["InfoMessage"] = "If the email exists, you will receive a new password.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error processing request. Please try again.");
                return View();
            }
        }

        // GET: /Login/AccessDenied
        public IActionResult AccessDenied()
        {
            return View();
        }

        // POST: /Login/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();

            TempData["SuccessMessage"] = "Successfully logged out.";
            return RedirectToAction("Index", "Login");
        }


        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.UserId.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2),
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetInt32("UserId", user.UserId);
        }

        private string GeneratePassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task SendNewPasswordEmail(string destinationEmail, string userName, string newPassword)
        {
            var mail = new MailMessage();
            mail.From = new MailAddress(EmailFrom, "TicketHub");
            mail.To.Add(destinationEmail);
            mail.Subject = "New Password - TicketHub";
            mail.IsBodyHtml = true;
            mail.Body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background: #f5f5f5; margin: 0; padding: 20px; }}
        .container {{ max-width: 500px; margin: 0 auto; background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.1); }}
        .header {{ background: #000; color: #fff; padding: 25px; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 30px; }}
        .content p {{ color: #555; line-height: 1.6; margin: 0 0 15px; }}
        .password-box {{ background: #f8f8f8; border: 2px dashed #e31837; border-radius: 8px; padding: 20px; text-align: center; margin: 20px 0; }}
        .password {{ font-size: 24px; font-weight: bold; color: #e31837; letter-spacing: 2px; font-family: monospace; }}
        .info {{ background: #e3f2fd; padding: 12px; border-radius: 6px; font-size: 13px; color: #1565c0; }}
        .footer {{ background: #f5f5f5; padding: 15px; text-align: center; color: #888; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎫 TicketHub</h1>
        </div>
        <div class='content'>
            <p>Hello <strong>{userName}</strong>,</p>
            <p>Your new temporary password is:</p>
            <div class='password-box'>
                <span class='password'>{newPassword}</span>
            </div>
            <p class='info'>ℹ️ When you log in, you will be asked to create a new personalized password.</p>
        </div>
        <div class='footer'>
            © 2025 TicketHub
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient("smtp.gmail.com", 587);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(EmailFrom, EmailPassword);
            await client.SendMailAsync(mail);
        }

        private IActionResult RedirectToDashboard(string role = null)
        {
            role ??= User.FindFirst(ClaimTypes.Role)?.Value;

            return role switch
            {
                "Admin" => RedirectToAction("Index", "Events"),
                "Worker" => RedirectToAction("Index", "Workers"),
                "Client" => RedirectToAction("Index", "ClientEvents"),
                _ => RedirectToAction("Index", "Home")
            };
        }
    }
}
