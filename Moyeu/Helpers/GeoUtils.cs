using System;
using System.IO;

namespace Moyeu
{
	public class GeoUtils
	{
		// Use spherical law of cosines
		public static double Distance (GeoPoint p1, GeoPoint p2)
		{
			const int EarthRadius = 6371;
			return Math.Acos (Math.Sin (DegToRad (p1.Lat)) * Math.Sin (DegToRad (p2.Lat)) +
			                  Math.Cos (DegToRad (p1.Lat)) * Math.Cos (DegToRad (p2.Lat)) *
			                  Math.Cos (DegToRad (p2.Lon) - DegToRad (p1.Lon))) * EarthRadius;
		}

		static double DegToRad (double deg)
		{
			return deg * Math.PI / 180;
		}

		public static void DumpLocation (GeoPoint p, Stream stream)
		{
			using (var writer = new BinaryWriter (stream, System.Text.Encoding.UTF8, true)) {
				writer.Write (p.Lat);
				writer.Write (p.Lon);
			}
		}

		public static GeoPoint ParseFromStream (Stream stream)
		{
			using (var reader = new BinaryReader (stream, System.Text.Encoding.UTF8, true)) {
				return new GeoPoint {
					Lat = reader.ReadDouble (),
					Lon = reader.ReadDouble ()
				};
			}
		}

		static bool UseMiles {
			get { return System.Globalization.CultureInfo.CurrentCulture.Name == "en-US"; }
		}

		public static string GetUnitForDistance (double distance)
		{
			return UseMiles ? "mi" : (distance >= 1000 ? "km" : "m");
		}

		public static string GetDisplayDistance (double distance, bool strictValue = false)
		{
			var isMiles = UseMiles;
			var display = (isMiles ? distance * 0.00062137 : (distance >= 1000 ? (distance / 1000) : distance)).ToString (isMiles ? "N1" : "N0");
			return display == "0" && !strictValue ? "<1" : display;
		}
	}
}

