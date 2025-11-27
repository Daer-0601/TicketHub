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
using iText.Kernel.Colors;
using iText.Layout.Properties;
using iText.Kernel.Geom;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Layout.Borders;
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

                    };

                    _context.Tickets.Add(ticket);
                    await _context.SaveChangesAsync();

                    // Generar QR
                    ticket.QrCode = GenerarQR($"{evento.EventId}-{ticket.TicketId}");
                    ticket.Sector = sector; // Asignar el sector para el PDF

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

            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf, PageSize.A4);
            document.SetMargins(40, 40, 40, 40);

            // Colores
            Color headerColor = new DeviceRgb(41, 128, 185); // Azul
            Color borderColor = new DeviceRgb(200, 200, 200); // Gris claro
            Color textColor = new DeviceRgb(44, 62, 80); // Gris oscuro
            Color successColor = new DeviceRgb(39, 174, 96); // Verde

            // Fuente base
            PdfFont baseFont = PdfFontFactory.CreateFont(StandardFontFamilies.HELVETICA);

            // Encabezado del documento
            Paragraph header = new Paragraph("ENTRADAS")
                .SetFont(baseFont)
                .SetFontSize(26)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontColor(headerColor)
                .SetMarginBottom(20);
            document.Add(header);

            // Información del evento en una caja con borde
            Div eventInfoBox = new Div()
                .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                .SetBorder(new SolidBorder(borderColor, 2))
                .SetPadding(15)
                .SetMarginBottom(25);

            Paragraph eventTitle = new Paragraph(evento.Name)
                .SetFont(baseFont)
                .SetFontSize(22)
                .SetFontColor(headerColor)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(10);
            eventInfoBox.Add(eventTitle);

            Table eventTable = new Table(2).UseAllAvailableWidth();
            eventTable.SetMarginBottom(10);

            // Fecha
            Cell dateLabel = new Cell().Add(new Paragraph("Fecha:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
            Cell dateValue = new Cell().Add(new Paragraph(evento.Date.ToString("dddd, dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES"))).SetFont(baseFont));
            eventTable.AddCell(dateLabel);
            eventTable.AddCell(dateValue);

            // Hora
            Cell timeLabel = new Cell().Add(new Paragraph("Hora:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
            Cell timeValue = new Cell().Add(new Paragraph(evento.Time.ToString(@"hh\:mm")).SetFont(baseFont));
            eventTable.AddCell(timeLabel);
            eventTable.AddCell(timeValue);

            // Ubicación
            Cell locationLabel = new Cell().Add(new Paragraph("Ubicacion:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
            Cell locationValue = new Cell().Add(new Paragraph(evento.Location).SetFont(baseFont));
            eventTable.AddCell(locationLabel);
            eventTable.AddCell(locationValue);

            eventInfoBox.Add(eventTable);
            document.Add(eventInfoBox);

            // Generar cada entrada
            int ticketNumber = 1;
            foreach (var ticket in tickets)
            {
                // Caja para cada entrada con borde
                Div ticketBox = new Div()
                    .SetBorder(new SolidBorder(headerColor, 3))
                    .SetPadding(20)
                    .SetMarginBottom(25)
                    .SetBackgroundColor(new DeviceRgb(255, 255, 255));

                // Título de la entrada
                Paragraph ticketTitle = new Paragraph($"ENTRADA #{ticketNumber}")
                    .SetFont(baseFont)
                    .SetFontSize(20)
                    .SetFontColor(headerColor)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(15);
                ticketBox.Add(ticketTitle);

                // Tabla con información del ticket
                Table ticketTable = new Table(2).UseAllAvailableWidth();
                ticketTable.SetMarginBottom(15);

                // Ticket ID
                Cell idLabel = new Cell().Add(new Paragraph("Ticket ID:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
                Cell idValue = new Cell().Add(new Paragraph($"#{ticket.TicketId}").SetFont(baseFont).SetFontSize(12));
                ticketTable.AddCell(idLabel);
                ticketTable.AddCell(idValue);

                // Sector
                Cell sectorLabel = new Cell().Add(new Paragraph("Sector:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
                Cell sectorValue = new Cell().Add(new Paragraph(ticket.Sector?.Name ?? "N/A").SetFont(baseFont).SetFontSize(12));
                ticketTable.AddCell(sectorLabel);
                ticketTable.AddCell(sectorValue);

                // Precio
                Cell priceLabel = new Cell().Add(new Paragraph("Precio:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
                string precioTexto = "$" + ticket.Price.ToString("F2");
                Cell priceValue = new Cell().Add(new Paragraph(precioTexto).SetFont(baseFont).SetFontSize(16).SetFontColor(successColor));
                ticketTable.AddCell(priceLabel);
                ticketTable.AddCell(priceValue);

                ticketBox.Add(ticketTable);

                // Insertar imagen QR centrada
                if (!string.IsNullOrEmpty(ticket.QrCode))
                {
                    try
                    {
                        string base64Data = ticket.QrCode.Replace("data:image/png;base64,", "");
                        byte[] qrBytes = Convert.FromBase64String(base64Data);
                        
                        ImageData imageData = ImageDataFactory.Create(qrBytes);
                        Image qrImage = new Image(imageData);
                        
                        // Tamaño del QR más grande y centrado
                        qrImage.SetWidth(150);
                        qrImage.SetHeight(150);
                        qrImage.SetHorizontalAlignment(HorizontalAlignment.CENTER);
                        qrImage.SetMarginTop(10);
                        qrImage.SetMarginBottom(10);
                        
                        ticketBox.Add(qrImage);

                        // Texto debajo del QR
                        Paragraph qrText = new Paragraph("Escanea este codigo QR para validar tu entrada")
                            .SetFont(baseFont)
                            .SetFontSize(9)
                            .SetFontColor(new DeviceRgb(150, 150, 150))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetMarginTop(5);
                        ticketBox.Add(qrText);
                    }
                    catch (Exception ex)
                    {
                        Paragraph errorText = new Paragraph($"Error al generar QR: {ex.Message}")
                            .SetFontColor(ColorConstants.RED)
                            .SetFontSize(10);
                        ticketBox.Add(errorText);
                    }
                }

                // Línea decorativa al final
                Div separator = new Div()
                    .SetHeight(2)
                    .SetBackgroundColor(headerColor)
                    .SetMarginTop(15);
                ticketBox.Add(separator);

                document.Add(ticketBox);
                ticketNumber++;
            }

            // Pie de página
            Paragraph footer = new Paragraph("Gracias por tu compra. Presenta este documento al ingresar al evento.")
                .SetFont(baseFont)
                .SetFontSize(10)
                .SetFontColor(new DeviceRgb(150, 150, 150))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(20);
            document.Add(footer);

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
