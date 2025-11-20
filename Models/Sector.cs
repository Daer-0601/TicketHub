using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models
{
	public class Sector
	{
		[Key]
		public int SectorId { get; set; }

		[Required]
		[StringLength(50)]
		public string Name { get; set; } = string.Empty;

		[Required]
		[Column(TypeName = "decimal(10,2)")]
		public decimal Price { get; set; }

		[Required]
		[Range(1, int.MaxValue)]
		public int Capacity { get; set; }

		// Relación con el evento
		[ForeignKey("Event")]
		public int EventId { get; set; }
		public Event? Event { get; set; }

		public ICollection<Ticket>? Tickets { get; set; }
	}


}
