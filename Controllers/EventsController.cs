using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;

namespace ProyectoFinal.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public EventsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Sectors)
                .OrderByDescending(e => e.Date)
                .ToListAsync();
            return View(events);
        }

        // GET: Events/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var evt = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (evt == null) return NotFound();
            return View(evt);
        }

        // GET: Events/Create
        public IActionResult Create()
        {
            var evt = new Event
            {
                Date = DateTime.Today,
                Time = new TimeSpan(20, 0, 0),
                Sectors = new List<Sector>()
            };
            return View(evt);
        }

        // POST: Events/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Event evt, IFormFile? imageFile)
        {
            if (evt.Sectors != null)
            {
                evt.Sectors = evt.Sectors
                    .Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.Price > 0 && s.Capacity > 0)
                    .ToList();
            }

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath, "images/events");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    await imageFile.CopyToAsync(stream);

                    evt.ImageUrl = "/images/events/" + uniqueName;
                }

                _context.Add(evt);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(evt);
        }

        // GET: Events/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var evt = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (evt == null) return NotFound();
            return View(evt);
        }

        // POST: Events/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Event evt, IFormFile? newImage)
        {
            if (id != evt.EventId)
                return NotFound();

            // Filtrar sectores válidos
            evt.Sectors = evt.Sectors?
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.Price > 0 && s.Capacity > 0)
                .ToList();

            if (ModelState.IsValid)
            {
                try
                {
                    // Procesar nueva imagen si se sube
                    if (newImage != null && newImage.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_env.WebRootPath, "images/events");
                        Directory.CreateDirectory(uploadsFolder);

                        string uniqueName = Guid.NewGuid().ToString() + Path.GetExtension(newImage.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueName);

                        using var stream = new FileStream(filePath, FileMode.Create);
                        await newImage.CopyToAsync(stream);

                        evt.ImageUrl = "/images/events/" + uniqueName;
                    }

                    // Actualizar solo campos del evento
                    var existingEvent = await _context.Events
                        .Include(e => e.Sectors)
                        .FirstOrDefaultAsync(e => e.EventId == id);

                    if (existingEvent == null)
                        return NotFound();

                    existingEvent.Name = evt.Name;
                    existingEvent.Date = evt.Date;
                    existingEvent.Time = evt.Time;
                    existingEvent.Location = evt.Location;
                    if (!string.IsNullOrEmpty(evt.ImageUrl))
                        existingEvent.ImageUrl = evt.ImageUrl;

                    // Manejar sectores
                    var sectorsToRemove = existingEvent.Sectors
                        .Where(s => !evt.Sectors.Any(x => x.SectorId == s.SectorId))
                        .ToList();
                    _context.Sectors.RemoveRange(sectorsToRemove);

                    foreach (var s in evt.Sectors)
                    {
                        s.EventId = existingEvent.EventId;
                        if (s.SectorId == 0)
                            _context.Sectors.Add(s);
                        else
                        {
                            var existingSector = existingEvent.Sectors.FirstOrDefault(x => x.SectorId == s.SectorId);
                            if (existingSector != null)
                            {
                                existingSector.Name = s.Name;
                                existingSector.Price = s.Price;
                                existingSector.Capacity = s.Capacity;
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(evt.EventId))
                        return NotFound();
                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(evt);
        }


        // GET: Events/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var evt = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (evt == null)
                return NotFound();

            return View(evt);
        }

        // POST: Events/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var evt = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (evt != null)
            {
                if (evt.Sectors != null)
                    _context.Sectors.RemoveRange(evt.Sectors);

                _context.Events.Remove(evt);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }


        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.EventId == id);
        }

        // GET: Events/Reports
        public async Task<IActionResult> Reports()
        {
            var events = await _context.Events
                .Include(e => e.Sectors)
                .Include(e => e.Tickets)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            // Calcular estadísticas generales
            var totalTicketsSold = await _context.Tickets.CountAsync();
            var totalTicketsScanned = await _context.Tickets.CountAsync(t => t.ScannedAt != null);
            var totalRevenue = await _context.Tickets.SumAsync(t => t.Price);

            ViewBag.TotalTicketsSold = totalTicketsSold;
            ViewBag.TotalTicketsScanned = totalTicketsScanned;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.PendingEntry = totalTicketsSold - totalTicketsScanned;

            return View(events);
        }

        // GET: Events/EventReport/5
        public async Task<IActionResult> EventReport(int? id)
        {
            if (id == null) return NotFound();

            var evt = await _context.Events
                .Include(e => e.Sectors)
                .Include(e => e.Tickets)
                    .ThenInclude(t => t.Sector)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (evt == null) return NotFound();

            return View(evt);
        }
    }
}
