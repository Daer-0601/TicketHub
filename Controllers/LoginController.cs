using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace ProyectoFinal.Controllers
{
    public class LoginController : Controller
    {
        private readonly ApplicationDbContext _context;

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

                // Crear claims identity
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

                TempData["SuccessMessage"] = $"Bienvenido, {user.UserName}!";
                return RedirectToDashboard(user.Role);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error al iniciar sesión. Intente nuevamente.");
                return View(loginViewModel);
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
                // Verificar si el usuario ya existe
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

                // Asignar rol por defecto (Client)
                newUser.Role = "Client";

                // Guardar nuevo usuario
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Registro exitoso. Ahora puede iniciar sesión.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
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
                    var resetToken = Guid.NewGuid().ToString();
                    HttpContext.Session.SetString("ResetToken", resetToken);
                    HttpContext.Session.SetString("ResetEmail", email);
                    HttpContext.Session.SetString("ResetTokenExpiry", DateTime.Now.AddMinutes(30).ToString());

                    TempData["InfoMessage"] = "Se han enviado instrucciones a su correo electrónico para restablecer la contraseña.";
                    return RedirectToAction("ResetPassword");
                }
                else
                {
                    TempData["InfoMessage"] = "Si el email existe en nuestro sistema, recibirá instrucciones para restablecer su contraseña.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error al procesar la solicitud. Intente nuevamente.");
                return View();
            }
        }

        // GET: /Login/ResetPassword
        public IActionResult ResetPassword()
        {
            var token = HttpContext.Session.GetString("ResetToken");
            var email = HttpContext.Session.GetString("ResetEmail");
            var expiry = HttpContext.Session.GetString("ResetTokenExpiry");

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(expiry))
            {
                TempData["ErrorMessage"] = "El enlace de restablecimiento ha expirado o es inválido.";
                return RedirectToAction("ForgotPassword");
            }

            if (DateTime.Parse(expiry) < DateTime.Now)
            {
                TempData["ErrorMessage"] = "El enlace de restablecimiento ha expirado.";
                return RedirectToAction("ForgotPassword");
            }

            return View();
        }

        // POST: /Login/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string newPassword, string confirmPassword)
        {
            var token = HttpContext.Session.GetString("ResetToken");
            var email = HttpContext.Session.GetString("ResetEmail");
            var expiry = HttpContext.Session.GetString("ResetTokenExpiry");

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(expiry))
            {
                TempData["ErrorMessage"] = "El enlace de restablecimiento ha expirado o es inválido.";
                return RedirectToAction("ForgotPassword");
            }

            if (DateTime.Parse(expiry) < DateTime.Now)
            {
                TempData["ErrorMessage"] = "El enlace de restablecimiento ha expirado.";
                return RedirectToAction("ForgotPassword");
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
                return View();
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            {
                ModelState.AddModelError(string.Empty, "La contraseña debe tener al menos 8 caracteres.");
                return View();
            }

            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    user.Password = newPassword;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    HttpContext.Session.Remove("ResetToken");
                    HttpContext.Session.Remove("ResetEmail");
                    HttpContext.Session.Remove("ResetTokenExpiry");

                    TempData["SuccessMessage"] = "Contraseña restablecida exitosamente. Puede iniciar sesión con su nueva contraseña.";
                    return RedirectToAction("Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Error al restablecer la contraseña.";
                    return RedirectToAction("ForgotPassword");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error al restablecer la contraseña. Intente nuevamente.");
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

        // Métodos auxiliares privados
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