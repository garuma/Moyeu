using System;

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
	}
}

