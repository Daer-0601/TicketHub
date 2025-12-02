using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using Microsoft.AspNetCore.Authorization;

namespace ProyectoFinal.Controllers
{
    [Authorize(Roles = "Worker")]
    public class WorkersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WorkersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Workers/Index
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Sectors)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            return View(events);
        }

        // POST: Workers/Scan
        [HttpPost]
        public async Task<IActionResult> Scan(string qrCode, int eventId, int sectorId)
        {
            if (string.IsNullOrEmpty(qrCode))
            {
                return Json(new { success = false, message = "Empty QR code" });
            }

            try
            {
                // Parse the QR code (format: "EventId-TicketId")
                var parts = qrCode.Split('-');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int qrEventId) || !int.TryParse(parts[1], out int ticketId))
                {
                    return Json(new { success = false, message = "Invalid QR code", status = "invalid" });
                }

                // Find the ticket
                var ticket = await _context.Tickets
                    .Include(t => t.Event)
                    .Include(t => t.Sector)
                    .FirstOrDefaultAsync(t => t.TicketId == ticketId);

                if (ticket == null)
                {
                    return Json(new { success = false, message = "Ticket not found", status = "notfound" });
                }

                // Check if the QR event matches the selected one
                if (qrEventId != eventId)
                {
                    return Json(new { 
                        success = false, 
                        message = $"This ticket belongs to another event (Event ID: {qrEventId})", 
                        status = "wrongevent",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            eventName = ticket.Event?.Name ?? "N/A",
                            sectorName = ticket.Sector?.Name ?? "N/A"
                        }
                    });
                }

                // Check if the ticket sector matches the selected one
                if (ticket.SectorId != sectorId)
                {
                    return Json(new { 
                        success = false, 
                        message = $"This ticket is for another sector. Ticket sector: {ticket.Sector?.Name ?? "N/A"}", 
                        status = "wrongsector",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            eventName = ticket.Event?.Name ?? "N/A",
                            sectorName = ticket.Sector?.Name ?? "N/A",
                            correctSectorId = ticket.SectorId
                        }
                    });
                }

                // Check if the ticket is valid
                if (!ticket.IsValid)
                {
                    return Json(new { 
                        success = false, 
                        message = "Invalid ticket", 
                        status = "invalid",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            scannedAt = ticket.ScannedAt
                        }
                    });
                }

                // Check if already scanned
                if (ticket.ScannedAt.HasValue)
                {
                    return Json(new { 
                        success = false, 
                        message = $"This ticket was already scanned on {ticket.ScannedAt.Value:MM/dd/yyyy HH:mm:ss}", 
                        status = "alreadyscanned",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            scannedAt = ticket.ScannedAt.Value
                        }
                    });
                }

                // Mark as scanned
                ticket.ScannedAt = DateTime.Now;
                _context.Tickets.Update(ticket);
                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Valid ticket scanned successfully", 
                    status = "valid",
                    ticketInfo = new {
                        ticketId = ticket.TicketId,
                        eventName = ticket.Event?.Name ?? "N/A",
                        sectorName = ticket.Sector?.Name ?? "N/A",
                        price = ticket.Price,
                        scannedAt = ticket.ScannedAt.Value
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Processing error: {ex.Message}", status = "error" });
            }
        }

        // GET: Workers/GetSectors
        [HttpGet]
        public async Task<IActionResult> GetSectors(int eventId)
        {
            var sectors = await _context.Sectors
                .Where(s => s.EventId == eventId)
                .OrderBy(s => s.Name)
                .Select(s => new { s.SectorId, s.Name })
                .ToListAsync();

            return Json(sectors);
        }
    }
}
