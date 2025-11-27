using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal.Models
{
	public class Ticket
	{
		[Key]
		public int TicketId { get; set; }

		[Required]
		public string QrCode { get; set; } = string.Empty;

		[Required]
		[ForeignKey("Sector")]
		public int SectorId { get; set; }
		public Sector? Sector { get; set; }

		[Required]
		[Column(TypeName = "decimal(10,2)")]
		public decimal Price { get; set; }

		public bool IsValid { get; set; } = true;
		public DateTime? ScannedAt { get; set; }

		[Required]
		[ForeignKey("Event")]
		public int EventId { get; set; }
		public Event? Event { get; set; }
	}

}
