using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.Widget;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Content;

using LatLng = Android.Gms.Maps.Model.LatLng;

namespace Moyeu
{
	public class GoogleApis
	{
		public const int MaxStreetViewSize = 640;

		const string ApiKey = "...";

		public static string MakeStreetViewUrl (GeoPoint position, int width, int height)
		{
			var parameters = new Dictionary<string, string> {
				{ "key", ApiKey },
				{ "sensor", "false" },
				{ "size", Math.Min (width, MaxStreetViewSize) + "x" + Math.Min (height, MaxStreetViewSize) },
				{ "location", position.Lat.ToSafeString () + "," + position.Lon.ToSafeString () },
				{ "pitch", "-10" },
			};

			return BuildUrl ("https://maps.googleapis.com/maps/api/streetview?", parameters);
		}

		public static string MakeMapUrl (GeoPoint location, int width, int height)
		{
			var zoom = "17";
			var markerSize = "size:med";
			var parameters = new Dictionary<string, string> {
				{ "key", ApiKey },
				{ "zoom", zoom },
				{ "sensor", "true" },
				{ "size", width + "x" + height },
				{ "center", location.Lat + "," + location.Lon },
				{ "markers", markerSize + "|" + location.Lat + "," + location.Lon }
			};

			return BuildUrl ("https://maps.googleapis.com/maps/api/staticmap?",
			                 parameters);
		}

		static string BuildUrl (string baseUrl, Dictionary<string, string> parameters)
		{
			return baseUrl + string.Join ("&", parameters.Select (pvp => pvp.Key + "=" + pvp.Value));
		}
	}
}

