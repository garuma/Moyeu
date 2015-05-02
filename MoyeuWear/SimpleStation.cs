using System;
using Android.Graphics;

namespace MoyeuWear
{
	public struct SimpleStation
	{
		public int Id { get; set; }

		public string Primary { get; set; }
		public string Secondary { get; set; }

		public double Distance { get; set; }
		public double Lat { get; set; }
		public double Lon { get; set; }

		public bool IsFavorite { get; set; }

		public int Bikes { get; set; }
		public int Racks { get; set; }

		public Bitmap Background { get; set; }
	}
}

