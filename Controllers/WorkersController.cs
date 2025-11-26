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
                return Json(new { success = false, message = "Código QR vacío" });
            }

            try
            {
                // Parsear el código QR (formato: "EventId-TicketId")
                var parts = qrCode.Split('-');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int qrEventId) || !int.TryParse(parts[1], out int ticketId))
                {
                    return Json(new { success = false, message = "Código QR inválido", status = "invalid" });
                }

                // Buscar el ticket
                var ticket = await _context.Tickets
                    .Include(t => t.Event)
                    .Include(t => t.Sector)
                    .FirstOrDefaultAsync(t => t.TicketId == ticketId);

                if (ticket == null)
                {
                    return Json(new { success = false, message = "Ticket no encontrado", status = "notfound" });
                }

                // Verificar si el evento del QR coincide con el seleccionado
                if (qrEventId != eventId)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Este ticket pertenece a otro evento (Evento ID: {qrEventId})", 
                        status = "wrongevent",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            eventName = ticket.Event?.Name ?? "N/A",
                            sectorName = ticket.Sector?.Name ?? "N/A"
                        }
                    });
                }

                // Verificar si el sector del ticket coincide con el seleccionado
                if (ticket.SectorId != sectorId)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Este ticket es para otro sector. Sector del ticket: {ticket.Sector?.Name ?? "N/A"}", 
                        status = "wrongsector",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            eventName = ticket.Event?.Name ?? "N/A",
                            sectorName = ticket.Sector?.Name ?? "N/A",
                            correctSectorId = ticket.SectorId
                        }
                    });
                }

                // Verificar si el ticket es válido
                if (!ticket.IsValid)
                {
                    return Json(new { 
                        success = false, 
                        message = "Ticket inválido", 
                        status = "invalid",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            scannedAt = ticket.ScannedAt
                        }
                    });
                }

                // Verificar si ya fue escaneado
                if (ticket.ScannedAt.HasValue)
                {
                    return Json(new { 
                        success = false, 
                        message = $"Este ticket ya fue escaneado el {ticket.ScannedAt.Value:dd/MM/yyyy HH:mm:ss}", 
                        status = "alreadyscanned",
                        ticketInfo = new {
                            ticketId = ticket.TicketId,
                            scannedAt = ticket.ScannedAt.Value
                        }
                    });
                }

                // Marcar como escaneado
                ticket.ScannedAt = DateTime.Now;
                _context.Tickets.Update(ticket);
                await _context.SaveChangesAsync();

                return Json(new { 
                    success = true, 
                    message = "Entrada válida y escaneada correctamente", 
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
                return Json(new { success = false, message = $"Error al procesar: {ex.Message}", status = "error" });
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

