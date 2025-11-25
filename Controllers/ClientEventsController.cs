using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal.Data;
using ProyectoFinal.Models;
using QRCoder;
using System.Net;
using System.Net.Mail;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.IO.Image;
using System.IO;
using System.Collections.Generic;


namespace ProyectoFinal.Controllers
{
    public class ClientEventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClientEventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================================
        // 1. LISTA DE EVENTOS
        // =============================================
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Sectors)
                .ToListAsync();

            return View(events);
        }

        // =============================================
        // 2. DETALLES DEL EVENTO
        // =============================================
        public async Task<IActionResult> Details(int id)
        {
            var evento = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (evento == null)
                return NotFound();

            return View(evento);
        }

        // =============================================
        // 3. PROCESO DE COMPRA
        // =============================================
        [HttpPost]
        public async Task<IActionResult> Buy(int eventId, Dictionary<int, int> cantidades, string email)
        {
            var evento = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (evento == null)
                return NotFound();

            List<Ticket> nuevosTickets = new();

            foreach (var item in cantidades)
            {
                int sectorId = item.Key;
                int cantidad = item.Value;

                if (cantidad <= 0) continue;

                var sector = await _context.Sectors.FindAsync(sectorId);
                if (sector == null) continue;

                for (int i = 0; i < cantidad; i++)
                {
                    Ticket ticket = new Ticket
                    {
                        EventId = eventId,
                        SectorId = sectorId,
                        Price = sector.Price,
                        UserId = 1, // usuario por defecto
                    };

                    _context.Tickets.Add(ticket);
                    await _context.SaveChangesAsync();

                    // Generar QR
                    ticket.QrCode = GenerarQR($"{evento.EventId}-{ticket.TicketId}");

                    nuevosTickets.Add(ticket);
                }
            }

            await _context.SaveChangesAsync();

            EnviarTicketsPorEmail(email, nuevosTickets, evento);

            return RedirectToAction("Success");
        }

        // =============================================
        // 4. PÁGINA DE ÉXITO
        // =============================================
        public IActionResult Success()
        {
            return View();
        }


        // =============================================
        // GENERAR QR EN BASE64
        // =============================================
        private string GenerarQR(string texto)
        {
            QRCodeGenerator qr = new QRCodeGenerator();
            QRCodeData data = qr.CreateQrCode(texto, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode png = new PngByteQRCode(data);
            byte[] img = png.GetGraphic(10);

            return $"data:image/png;base64,{Convert.ToBase64String(img)}";
        }



        // =============================================
        // GENERAR PDF PARA ENVIAR EN EL EMAIL
        // =============================================

        public byte[] GenerarPDFEntradas(Event evento, List<Ticket> tickets)
        {
            using MemoryStream ms = new MemoryStream();

            // PdfWriter y PdfDocument sin SmartMode (opción no disponible en C#)
            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf);

            // Información del evento
            document.Add(new Paragraph($"Evento: {evento.Name}").SetFontSize(16));
            document.Add(new Paragraph($"Fecha: {evento.Date:yyyy-MM-dd}"));
            document.Add(new Paragraph($"Hora: {evento.Time:hh\\:mm}"));
            document.Add(new Paragraph($"Ubicación: {evento.Location}"));
            document.Add(new Paragraph(" "));

            foreach (var ticket in tickets)
            {
                document.Add(new Paragraph($"Ticket ID: {ticket.TicketId}"));
                document.Add(new Paragraph($"Sector: {ticket.Sector?.Name ?? "N/A"}"));
                document.Add(new Paragraph($"Precio: ${ticket.Price}"));

                // Insertar imagen QR
                if (!string.IsNullOrEmpty(ticket.QrCode))
                {
                    string base64Data = ticket.QrCode.Replace("data:image/png;base64,", "");
                    byte[] qrBytes = Convert.FromBase64String(base64Data);
                    using MemoryStream qrStream = new MemoryStream(qrBytes);
                    iText.Layout.Element.Image qrImage = new iText.Layout.Element.Image(ImageDataFactory.Create(qrStream))
                        .SetWidth(100)
                        .SetHeight(100);
                    document.Add(qrImage);
                }

                // Separación entre tickets
                document.Add(new LineSeparator(new iText.Layout.Borders.SolidBorder(1)));
                document.Add(new Paragraph(" "));
            }

            document.Close();
            return ms.ToArray();
        }



        // =============================================
        // ENVIAR EMAIL CON PDF ADJUNTO
        // =============================================
        public void EnviarTicketsPorEmail(string emailDestino, List<Ticket> tickets, Event evento)
        {
            byte[] pdfBytes = GenerarPDFEntradas(evento, tickets);

            MailMessage mail = new MailMessage();
            mail.From = new MailAddress("andrescaleraa7@gmail.com");
            mail.To.Add(emailDestino);
            mail.Subject = "Tus Tickets - " + evento.Name;
            mail.Body = "Gracias por tu compra. Tus entradas están adjuntas en formato PDF.";

            mail.Attachments.Add(new Attachment(new MemoryStream(pdfBytes), "Entradas.pdf"));

            using (SmtpClient client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential("andrescaleraa7@gmail.com", "aytu wnvj vyqm pntn");
                client.Send(mail);
            }
        }

    }
}
