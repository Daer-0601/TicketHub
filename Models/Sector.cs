using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models
{
	public class Sector
	{
		[Key]
		public int SectorId { get; set; }

		[Required(ErrorMessage = "Sector name is required.")]
		[StringLength(50, ErrorMessage = "Name cannot exceed 50 characters.")]
		[RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Name can only contain letters, numbers, and spaces.")]
		[Display(Name = "Sector Name")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Price is required.")]
		[Column(TypeName = "decimal(10,2)")]
		[Range(1, double.MaxValue, ErrorMessage = "Price must be at least 1.")]
		[Display(Name = "Price")]
		public decimal Price { get; set; }

		[Required(ErrorMessage = "Capacity is required.")]
		[Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
		[Display(Name = "Capacity")]
		public int Capacity { get; set; }

		// Relationship with the event
		[ForeignKey("Event")]
		public int EventId { get; set; }
		public Event? Event { get; set; }

		public ICollection<Ticket>? Tickets { get; set; }
	}
}
