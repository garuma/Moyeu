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
		static GoogleApis instance;

		Context context;
		HttpClient client = new HttpClient ();
		BitmapCache mapCache;
		Task<BitmapCache> mapCacheLoader;

		public static GoogleApis Obtain (Context context)
		{
			if (instance == null)
				instance = new GoogleApis (context);
			return instance;
		}

		private GoogleApis (Context context)
		{
			this.context = context;
			this.mapCacheLoader = Task.Run (new Func<BitmapCache> (LoadMapCache));
		}

		public BitmapCache MapCache {
			get {
				return mapCache ?? (mapCache = mapCacheLoader.Result);
			}
		}

		BitmapCache LoadMapCache ()
		{
			var round = !AndroidExtensions.IsMaterial;
			return BitmapCache.CreateCache (context, "MapsPictures", useRoundCorners: round);
		}

		async Task<Drawable> LoadInternal (string url, BitmapCache cache)
		{
			try {
				var data = await client.GetByteArrayAsync (url).ConfigureAwait (false);
				var bmp = BitmapFactory.DecodeByteArray (data, 0, data.Length);
				return cache.AddOrUpdate (url, bmp, TimeSpan.FromDays (90));
			} catch (Exception e) {
				e.Data ["Url"] = url;
				AnalyticsHelper.LogException ("GoogleApiDownloader", e);
				Android.Util.Log.Error ("GoogleApiDownloader", "For URL " + url + ". Exception: " + e.ToString ());
			}
			return null;
		}

		internal async void LoadMap (GeoPoint point, FavoriteView view, ImageView mapView, long version)
		{
			var url = MakeMapUrl (point);
			var drawable = await LoadInternal (url, MapCache);
			if (drawable != null && view.VersionNumber == version) {
				mapView.AlphaAnimate (0, duration: 150, endAction: () => {
					mapView.SetImageDrawable (drawable);
					mapView.AlphaAnimate (1, duration: 250);
				});
			}
		}

		public static string MakeStreetViewUrl (GeoPoint position, int width, int height)
		{
			var parameters = new Dictionary<string, string> {
				{ "key", ApiKey },
				{ "sensor", "false" },
				{ "size", Math.Min (width, MaxStreetViewSize) + "x" + Math.Min (height, MaxStreetViewSize) },
				{ "location", position.Lat.ToString () + "," + position.Lon.ToString () },
				{ "pitch", "-10" },
			};

			return BuildUrl ("https://maps.googleapis.com/maps/api/streetview?", parameters);
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

