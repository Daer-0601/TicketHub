using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using System.Net;
using System.Net.Mail;
using BCrypt.Net;

namespace ProyectoFinal.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Gmail credentials
        private const string EmailFrom = "andrescaleraa7@gmail.com";
        private const string EmailPassword = "aytu wnvj vyqm pntn";

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            // Remove Password from ModelState since we generate it automatically
            ModelState.Remove("Password");

            // Check if username already exists
            var existingUserName = await _context.Users.FirstOrDefaultAsync(u => u.UserName == user.UserName);
            if (existingUserName != null)
            {
                ModelState.AddModelError("UserName", "Username is already in use.");
            }

            // Check if email already exists
            var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Email address is already registered.");
            }

            if (ModelState.IsValid)
            {
                // Generate random password
                var generatedPassword = GeneratePassword();
                user.Password = BCrypt.Net.BCrypt.HashPassword(generatedPassword);
                user.MustChangePassword = true;

                _context.Add(user);
                await _context.SaveChangesAsync();

                // Send password by email
                try
                {
                    await SendWelcomeEmail(user.Email, user.UserName, generatedPassword, user.Role);
                    TempData["SuccessMessage"] = $"User '{user.UserName}' created successfully. Temporary password sent to {user.Email}.";
                }
                catch (Exception)
                {
                    TempData["SuccessMessage"] = $"User '{user.UserName}' created. Password: {generatedPassword} (Email could not be sent)";
                }

                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.UserId)
            {
                return NotFound();
            }

            // Check if username already exists (excluding current user)
            var existingUserName = await _context.Users.FirstOrDefaultAsync(u => u.UserName == user.UserName && u.UserId != id);
            if (existingUserName != null)
            {
                ModelState.AddModelError("UserName", "Username is already in use.");
            }

            // Check if email already exists (excluding current user)
            var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email && u.UserId != id);
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "Email address is already registered.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "User updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.UserId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                _context.Users.Remove(user);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "User deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }

        private string GeneratePassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task SendWelcomeEmail(string destinationEmail, string userName, string password, string role)
        {
            var mail = new MailMessage();
            mail.From = new MailAddress(EmailFrom, "TicketHub");
            mail.To.Add(destinationEmail);
            mail.Subject = "Welcome to TicketHub - Your Account Details";
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
        .credentials-box {{ background: #f8f8f8; border: 2px solid #e31837; border-radius: 8px; padding: 20px; margin: 20px 0; }}
        .credentials-box h3 {{ margin: 0 0 15px; color: #333; }}
        .credential-item {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #eee; }}
        .credential-item:last-child {{ border-bottom: none; }}
        .credential-label {{ color: #666; }}
        .credential-value {{ font-weight: bold; color: #e31837; font-family: monospace; }}
        .role-badge {{ display: inline-block; background: #e31837; color: #fff; padding: 4px 12px; border-radius: 20px; font-size: 12px; }}
        .info {{ background: #fff3cd; padding: 12px; border-radius: 6px; font-size: 13px; color: #856404; border-left: 4px solid #ffc107; }}
        .footer {{ background: #f5f5f5; padding: 15px; text-align: center; color: #888; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎫 Welcome to TicketHub</h1>
        </div>
        <div class='content'>
            <p>Hello <strong>{userName}</strong>,</p>
            <p>Your account has been created successfully. Below are your login credentials:</p>
            
            <div class='credentials-box'>
                <h3>Your Credentials</h3>
                <div class='credential-item'>
                    <span class='credential-label'>Username:</span>
                    <span class='credential-value'>{userName}</span>
                </div>
                <div class='credential-item'>
                    <span class='credential-label'>Temporary Password:</span>
                    <span class='credential-value'>{password}</span>
                </div>
                <div class='credential-item'>
                    <span class='credential-label'>Role:</span>
                    <span class='role-badge'>{role}</span>
                </div>
            </div>
            
            <p class='info'>⚠️ <strong>Important:</strong> You will be required to change your password when you log in for the first time.</p>
        </div>
        <div class='footer'>
            © 2025 TicketHub - Event Management System
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient("smtp.gmail.com", 587);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(EmailFrom, EmailPassword);
            await client.SendMailAsync(mail);
        }
    }
}
