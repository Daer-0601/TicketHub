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

    
        public async Task<IActionResult> Index()
        {
            var events = await _context.Events
                .Include(e => e.Sectors)
                .ToListAsync();

            return View(events);
        }

   
        public async Task<IActionResult> Details(int id)
        {
            var evento = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (evento == null)
                return NotFound();

           
            var availability = new Dictionary<int, int>();
            foreach (var sector in evento.Sectors)
            {
                int sold = await _context.Tickets.CountAsync(t => t.SectorId == sector.SectorId);
                availability[sector.SectorId] = sector.Capacity - sold;
            }
            ViewBag.Disponibilidad = availability;

            return View(evento);
        }


        [HttpPost]
        public async Task<IActionResult> Buy(int eventId, Dictionary<int, int> cantidades, string email)
        {
            var evento = await _context.Events
                .Include(e => e.Sectors)
                .FirstOrDefaultAsync(e => e.EventId == eventId);

            if (evento == null)
                return NotFound();

            foreach (var item in cantidades)
            {
                int sectorId = item.Key;
                int quantity = item.Value;

                if (quantity <= 0) continue;

                var sector = await _context.Sectors.FindAsync(sectorId);
                if (sector == null) continue;

                int ticketsSold = await _context.Tickets.CountAsync(t => t.SectorId == sectorId);
                int available = sector.Capacity - ticketsSold;

                if (quantity > available)
                {
                    TempData["ErrorMessage"] = $"Not enough tickets in {sector.Name}. Available: {available}";
                    return RedirectToAction("Details", new { id = eventId });
                }
            }

            List<Ticket> newTickets = new();

            foreach (var item in cantidades)
            {
                int sectorId = item.Key;
                int quantity = item.Value;

                if (quantity <= 0) continue;

                var sector = await _context.Sectors.FindAsync(sectorId);
                if (sector == null) continue;

                for (int i = 0; i < quantity; i++)
                {
                    Ticket ticket = new Ticket
                    {
                        EventId = eventId,
                        SectorId = sectorId,
                        Price = sector.Price,
                    };

                    _context.Tickets.Add(ticket);
                    await _context.SaveChangesAsync();

                    ticket.QrCode = GenerateQR($"{evento.EventId}-{ticket.TicketId}");
                    ticket.Sector = sector;

                    newTickets.Add(ticket);
                }
            }

            await _context.SaveChangesAsync();

            SendTicketsByEmail(email, newTickets, evento);

            return RedirectToAction("Success");
        }

   
        public IActionResult Success()
        {
            return View();
        }



        private string GenerateQR(string text)
        {
            QRCodeGenerator qr = new QRCodeGenerator();
            QRCodeData data = qr.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode png = new PngByteQRCode(data);
            byte[] img = png.GetGraphic(10);

            return $"data:image/png;base64,{Convert.ToBase64String(img)}";
        }




        public byte[] GenerateTicketsPDF(Event evento, List<Ticket> tickets)
        {
            using MemoryStream ms = new MemoryStream();

            PdfWriter writer = new PdfWriter(ms);
            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf, PageSize.A4);
            document.SetMargins(40, 40, 40, 40);


            Color headerColor = new DeviceRgb(41, 128, 185); 
            Color borderColor = new DeviceRgb(200, 200, 200);
            Color textColor = new DeviceRgb(44, 62, 80); 
            Color successColor = new DeviceRgb(39, 174, 96); 

     
            PdfFont baseFont = PdfFontFactory.CreateFont(StandardFontFamilies.HELVETICA);


            Paragraph header = new Paragraph("TICKETS")
                .SetFont(baseFont)
                .SetFontSize(26)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontColor(headerColor)
                .SetMarginBottom(20);
            document.Add(header);

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


            Cell dateLabel = new Cell().Add(new Paragraph("Date:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
            Cell dateValue = new Cell().Add(new Paragraph(evento.Date.ToString("dddd, MMMM dd, yyyy", new System.Globalization.CultureInfo("en-US"))).SetFont(baseFont));
            eventTable.AddCell(dateLabel);
            eventTable.AddCell(dateValue);

       
            Cell timeLabel = new Cell().Add(new Paragraph("Time:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
            Cell timeValue = new Cell().Add(new Paragraph(evento.Time.ToString(@"hh\:mm")).SetFont(baseFont));
            eventTable.AddCell(timeLabel);
            eventTable.AddCell(timeValue);

     
            Cell locationLabel = new Cell().Add(new Paragraph("Location:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
            Cell locationValue = new Cell().Add(new Paragraph(evento.Location).SetFont(baseFont));
            eventTable.AddCell(locationLabel);
            eventTable.AddCell(locationValue);

            eventInfoBox.Add(eventTable);
            document.Add(eventInfoBox);


            int ticketNumber = 1;
            foreach (var ticket in tickets)
            {
   
                Div ticketBox = new Div()
                    .SetBorder(new SolidBorder(headerColor, 3))
                    .SetPadding(20)
                    .SetMarginBottom(25)
                    .SetBackgroundColor(new DeviceRgb(255, 255, 255));

        
                Paragraph ticketTitle = new Paragraph($"TICKET #{ticketNumber}")
                    .SetFont(baseFont)
                    .SetFontSize(20)
                    .SetFontColor(headerColor)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(15);
                ticketBox.Add(ticketTitle);

                Table ticketTable = new Table(2).UseAllAvailableWidth();
                ticketTable.SetMarginBottom(15);


                Cell sectorLabel = new Cell().Add(new Paragraph("Sector:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
                Cell sectorValue = new Cell().Add(new Paragraph(ticket.Sector?.Name ?? "N/A").SetFont(baseFont).SetFontSize(12));
                ticketTable.AddCell(sectorLabel);
                ticketTable.AddCell(sectorValue);

     
                Cell priceLabel = new Cell().Add(new Paragraph("Price:").SetFont(baseFont).SetFontSize(12).SetFontColor(textColor));
                string priceText = "$" + ticket.Price.ToString("F2");
                Cell priceValue = new Cell().Add(new Paragraph(priceText).SetFont(baseFont).SetFontSize(16).SetFontColor(successColor));
                ticketTable.AddCell(priceLabel);
                ticketTable.AddCell(priceValue);

                ticketBox.Add(ticketTable);


                if (!string.IsNullOrEmpty(ticket.QrCode))
                {
                    try
                    {
                        string base64Data = ticket.QrCode.Replace("data:image/png;base64,", "");
                        byte[] qrBytes = Convert.FromBase64String(base64Data);
                        
                        ImageData imageData = ImageDataFactory.Create(qrBytes);
                        Image qrImage = new Image(imageData);
     
                        qrImage.SetWidth(150);
                        qrImage.SetHeight(150);
                        qrImage.SetHorizontalAlignment(HorizontalAlignment.CENTER);
                        qrImage.SetMarginTop(10);
                        qrImage.SetMarginBottom(10);
                        
                        ticketBox.Add(qrImage);

                
                        Paragraph qrText = new Paragraph("Scan this QR code to validate your ticket")
                            .SetFont(baseFont)
                            .SetFontSize(9)
                            .SetFontColor(new DeviceRgb(150, 150, 150))
                            .SetTextAlignment(TextAlignment.CENTER)
                            .SetMarginTop(5);
                        ticketBox.Add(qrText);
                    }
                    catch (Exception ex)
                    {
                        Paragraph errorText = new Paragraph($"Error generating QR: {ex.Message}")
                            .SetFontColor(ColorConstants.RED)
                            .SetFontSize(10);
                        ticketBox.Add(errorText);
                    }
                }

                Div separator = new Div()
                    .SetHeight(2)
                    .SetBackgroundColor(headerColor)
                    .SetMarginTop(15);
                ticketBox.Add(separator);

                document.Add(ticketBox);
                ticketNumber++;
            }

       
            Paragraph footer = new Paragraph("Thank you for your purchase. Present this document when entering the event.")
                .SetFont(baseFont)
                .SetFontSize(10)
                .SetFontColor(new DeviceRgb(150, 150, 150))
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(20);
            document.Add(footer);

            document.Close();
            return ms.ToArray();
        }



        public void SendTicketsByEmail(string destinationEmail, List<Ticket> tickets, Event evento)
        {
            byte[] pdfBytes = GenerateTicketsPDF(evento, tickets);

            MailMessage mail = new MailMessage();
            mail.From = new MailAddress("andrescaleraa7@gmail.com");
            mail.To.Add(destinationEmail);
            mail.Subject = "Your Tickets - " + evento.Name;
            mail.Body = "Thank you for your purchase. Your tickets are attached in PDF format.";

            mail.Attachments.Add(new Attachment(new MemoryStream(pdfBytes), "Tickets.pdf"));

            using (SmtpClient client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential("andrescaleraa7@gmail.com", "aytu wnvj vyqm pntn");
                client.Send(mail);
            }
        }

    }
}
