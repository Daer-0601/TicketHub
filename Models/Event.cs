using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal.Models
{
	public class Event
	{
		[Key]
		public int EventId { get; set; }

		[Required(ErrorMessage = "Event name is required.")]
		[StringLength(100, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 100 characters.")]
		[RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Name can only contain letters, numbers, and spaces.")]
		[Display(Name = "Event Name")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "Date is required.")]
		[DataType(DataType.Date)]
		[Display(Name = "Event Date")]
		public DateTime Date { get; set; }

		[Required(ErrorMessage = "Time is required.")]
		[Display(Name = "Event Time")]
		public TimeSpan Time { get; set; }

		[Required(ErrorMessage = "Location is required.")]
		[StringLength(150, ErrorMessage = "Location cannot exceed 150 characters.")]
		[RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Location can only contain letters, numbers, and spaces.")]
		[Display(Name = "Location")]
		public string Location { get; set; } = string.Empty;

		public string? ImageUrl { get; set; }

		public ICollection<Sector>? Sectors { get; set; }
		public ICollection<Ticket>? Tickets { get; set; }
	}
}
