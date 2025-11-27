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

        // Credenciales de Gmail
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
                    ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                    return View(loginViewModel);
                }

                // Verificar si debe cambiar contraseña
                if (user.MustChangePassword)
                {
                    // Guardar en sesión para la página de cambio
                    HttpContext.Session.SetInt32("ChangePasswordUserId", user.UserId);
                    HttpContext.Session.SetString("ChangePasswordUserName", user.UserName);
                    return RedirectToAction("ChangePassword");
                }

                // Login normal
                await SignInUser(user);
                return RedirectToDashboard(user.Role);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error al iniciar sesión. Intente nuevamente.");
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
                ModelState.AddModelError(string.Empty, "La contraseña debe tener al menos 8 caracteres.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
                return View();
            }

            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return RedirectToAction("Index");
                }

                // Actualizar contraseña y quitar la marca
                user.Password = newPassword;
                user.MustChangePassword = false;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Limpiar sesión temporal
                HttpContext.Session.Remove("ChangePasswordUserId");
                HttpContext.Session.Remove("ChangePasswordUserName");

                // Iniciar sesión automáticamente
                await SignInUser(user);
                return RedirectToDashboard(user.Role);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error al cambiar la contraseña. Intente nuevamente.");
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
                ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
                return View(newUser);
            }

            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.UserName == newUser.UserName || u.Email == newUser.Email);

                if (existingUser != null)
                {
                    if (existingUser.UserName == newUser.UserName)
                        ModelState.AddModelError(string.Empty, "El nombre de usuario ya está en uso.");
                    if (existingUser.Email == newUser.Email)
                        ModelState.AddModelError(string.Empty, "El correo electrónico ya está registrado.");
                    return View(newUser);
                }

                newUser.Role = "Client";
                newUser.MustChangePassword = false;
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registro exitoso. Ahora puede iniciar sesión.";
                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error al registrar el usuario. Intente nuevamente.");
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
                ModelState.AddModelError(string.Empty, "Por favor ingrese su correo electrónico.");
                return View();
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user != null)
                {
                    // Generar contraseña aleatoria
                    var nuevaPassword = GenerarPassword();

                    // Actualizar en BD y marcar para cambio obligatorio
                    user.Password = nuevaPassword;
                    user.MustChangePassword = true;  // <-- Obligar cambio en próximo login
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    // Enviar por email
                    await EnviarEmailNuevaPassword(email, user.UserName, nuevaPassword);

                    TempData["SuccessMessage"] = "Se ha enviado una nueva contraseña a su correo electrónico.";
                }
                else
                {
                    TempData["InfoMessage"] = "Si el email existe, recibirá una nueva contraseña.";
                }

                return RedirectToAction("Index");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Error al procesar la solicitud. Intente nuevamente.");
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

            TempData["SuccessMessage"] = "Sesión cerrada exitosamente.";
            return RedirectToAction("Index", "Login");
        }

        // =============================================
        // MÉTODOS PRIVADOS
        // =============================================

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

        private string GenerarPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task EnviarEmailNuevaPassword(string emailDestino, string nombreUsuario, string nuevaPassword)
        {
            var mail = new MailMessage();
            mail.From = new MailAddress(EmailFrom, "TicketHub");
            mail.To.Add(emailDestino);
            mail.Subject = "Nueva Contraseña - TicketHub";
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
            <p>Hola <strong>{nombreUsuario}</strong>,</p>
            <p>Tu nueva contraseña temporal es:</p>
            <div class='password-box'>
                <span class='password'>{nuevaPassword}</span>
            </div>
            <p class='info'>ℹ️ Al iniciar sesión, se te pedirá crear una nueva contraseña personalizada.</p>
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
