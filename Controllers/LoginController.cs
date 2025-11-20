using Microsoft.AspNetCore.Mvc;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using Microsoft.EntityFrameworkCore;

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
			return View();
		}

		// POST: /Login
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Index(User loginUser)
		{
			if (!ModelState.IsValid)
			{
				return View(loginUser);
			}

			// Buscar usuario en la base de datos
			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.UserName == loginUser.UserName && u.Password == loginUser.Password);

			if (user == null)
			{
				ModelState.AddModelError(string.Empty, "Invalid username or password.");
				return View(loginUser);
			}

			// Guardar datos en sesión (ejemplo simple, puedes usar Identity)
			HttpContext.Session.SetString("UserName", user.UserName);
			HttpContext.Session.SetString("Role", user.Role);

			// Redirigir según rol
			if (user.Role == "Admin")
			{
				return RedirectToAction("Index", "Events");
			}
			else if (user.Role == "Worker")
			{
				return RedirectToAction("Index", "Workers");
			}
			else
			{
				return RedirectToAction("Index", "Clients");
			}
		}

		// GET: /Logout
		public IActionResult Logout()
		{
			HttpContext.Session.Clear();
			return RedirectToAction("Index", "Login");
		}
	}
}
