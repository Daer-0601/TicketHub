using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;

namespace ProyectoFinal.Models
{
	public class Event
	{
		[Key]
		public int EventId { get; set; }

		[Required]
		[StringLength(100, MinimumLength = 3)]
		public string Name { get; set; } = string.Empty;

		[Required]
		public DateTime Date { get; set; }

		[Required]
		public TimeSpan Time { get; set; }

		[Required]
		[StringLength(150)]
		public string Location { get; set; } = string.Empty;

		public string? ImageUrl { get; set; }

		public ICollection<Sector>? Sectors { get; set; }
		public ICollection<Ticket>? Tickets { get; set; }
	}

}
