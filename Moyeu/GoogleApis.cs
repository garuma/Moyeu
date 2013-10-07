using System;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.Widget;
using Android.Graphics;
using Android.Content;

using LatLng = Android.Gms.Maps.Model.LatLng;

namespace Moyeu
{
	public class GoogleApis
	{
		public const int MaxStreetViewSize = 640;

		const string ApiKey = "...";
		static GoogleApis instance;

		Context context;
		HttpClient client = new HttpClient ();
		BitmapCache mapCache;
		BitmapCache streetViewCache;

		public static GoogleApis Obtain (Context context)
		{
			if (instance == null)
				instance = new GoogleApis (context);
			return instance;
		}

		private GoogleApis (Context context)
		{
			this.context = context;
		}

		public BitmapCache MapCache {
			get {
				return mapCache
					?? (mapCache = BitmapCache.CreateCache (context, "MapsPictures"));
			}
		}

		public BitmapCache StreetViewCache {
			get {
				return streetViewCache
					?? (streetViewCache = BitmapCache.CreateCache (context, "StreetViewPictures"));
			}
		}

		async Task<Bitmap> LoadInternal (string url, BitmapCache cache)
		{
			try {
				var data = await client.GetByteArrayAsync (url);
				var bmp = await BitmapFactory.DecodeByteArrayAsync (data, 0, data.Length);
				cache.AddOrUpdate (url, bmp, TimeSpan.FromDays (90));
				return bmp;
			} catch (Exception e) {
				Android.Util.Log.Error ("GoogleApiDownloader", "For URL " + url + ". Exception: " + e.ToString ());
			}
			return null;
		}

		internal async void LoadMap (GeoPoint point, FavoriteView view, ImageView mapView, long version)
		{
			var url = MakeMapUrl (point);
			var bmp = await LoadInternal (url, MapCache);
			if (bmp != null && view.VersionNumber == version) {
				mapView.AlphaAnimate (0, duration: 150, endAction: () => {
					mapView.SetImageDrawable (new RoundCornerDrawable (bmp));
					mapView.AlphaAnimate (1, duration: 250);
				});
			}
		}

		internal async void LoadStreetView (LatLng location, HubwayMapFragment frag, long stationID, ProgressBar spinner, ImageView img)
		{
			var url = MakeStreetViewUrl (img, location);
			var bmp = await LoadInternal (url, StreetViewCache);
			if (bmp != null && frag.CurrentShownId == stationID) {
				img.SetImageDrawable (new RoundCornerDrawable (bmp, cornerRadius: 3));
				img.Visibility = Android.Views.ViewStates.Visible;
				spinner.AlphaAnimate (0, duration: 250);
				img.AlphaAnimate (1, duration: 250);
			}
		}

		public static string MakeStreetViewUrl (ImageView view, LatLng position)
		{
			var parameters = new Dictionary<string, string> {
				{ "key", ApiKey },
				{ "sensor", "false" },
				{ "size", Math.Min (view.Width, MaxStreetViewSize) + "x" + Math.Min (view.Height, MaxStreetViewSize) },
				{ "location", position.Latitude.ToString () + "," + position.Longitude.ToString () },
				{ "pitch", "-10" },
			};

			return BuildUrl ("https://maps.googleapis.com/maps/api/streetview?",
			                 parameters);
		}

		public static string MakeMapUrl (GeoPoint location)
		{
			const int Width = 65;
			const int Height = 42;

			var parameters = new Dictionary<string, string> {
				{ "key", ApiKey },
				{ "zoom", "13" },
				{ "sensor", "true" },
				{ "size", Width.ToPixels () + "x" + Height.ToPixels () },
				{ "center", location.Lat + "," + location.Lon },
				{ "markers", "size:tiny|" + location.Lat + "," + location.Lon }
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

