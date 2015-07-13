
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using Android.Gms.Wearable;
using Android.Gms.Common.Apis;
using Android.Gms.Location;

namespace Moyeu
{
	[Service]
	[IntentFilter (new[] { "com.google.android.gms.wearable.BIND_LISTENER" })]
	public class WearService : WearableListenerService
	{
		const string SearchStationPath = "/moyeu/SearchNearestStations";
		const string ActionPrefixPath = "/moyeu/Action";

		const string FavoriteAction = "Favorite";
		const string NavigateAction = "Navigate";

		HttpClient client;
		Handler handler;

		public override void OnCreate ()
		{
			base.OnCreate ();
			this.handler = new Handler ();
			Android.Util.Log.Error ("WearIntegration", "Created");
		}

		public override void OnMessageReceived (IMessageEvent messageEvent)
		{
			base.OnMessageReceived (messageEvent);
			if (!messageEvent.Path.StartsWith (SearchStationPath)
			    && !messageEvent.Path.StartsWith (ActionPrefixPath))
				return;

			HandleMessage (messageEvent);
		}

		async void HandleMessage (IMessageEvent message)
		{
			try {
				Android.Util.Log.Info ("WearIntegration", "Received Message");
				var client = new GoogleApiClientBuilder (this)
					.AddApi (LocationServices.API)
					.AddApi (WearableClass.API)
					.Build ();
				
				var result = client.BlockingConnect (30, Java.Util.Concurrent.TimeUnit.Seconds);
				if (!result.IsSuccess)
					return;

				var path = message.Path;

				try {
					var stations = Hubway.Instance.LastStations;
					if (stations == null)
						stations = await Hubway.Instance.GetStations ();

					if (path.StartsWith (SearchStationPath)) {
						var lastLocation = LocationServices.FusedLocationApi.GetLastLocation (client);
						if (lastLocation == null)
							return;

						var currentPoint = new GeoPoint {
							Lat = lastLocation.Latitude,
							Lon = lastLocation.Longitude
						};
						var nearestStations = Hubway.GetStationsAround (stations, currentPoint, maxItems: 6, minDistance: double.MaxValue);
						var favManager = FavoriteManager.Obtain (this);
						var favorites = await favManager.GetFavoriteStationIdsAsync ();

						var request = PutDataMapRequest.Create (SearchStationPath + "/Answer");
						var map = request.DataMap;

						var stationMap = new List<DataMap> ();
						foreach (var station in nearestStations) {
							var itemMap = new DataMap ();

							itemMap.PutInt ("Id", station.Id);

							var asset = await CreateWearAssetFrom (station);
							itemMap.PutAsset ("Background", asset);

							string secondary;
							string primary = StationUtils.CutStationName (station.Name, out secondary);
							itemMap.PutString ("Primary", primary);
							itemMap.PutString ("Secondary", secondary);

							var distance = GeoUtils.Distance (currentPoint, station.Location);
							itemMap.PutDouble ("Distance", distance);
							itemMap.PutDouble ("Lat", station.Location.Lat);
							itemMap.PutDouble ("Lon", station.Location.Lon);

							itemMap.PutInt ("Bikes", station.BikeCount);
							itemMap.PutInt ("Racks", station.EmptySlotCount);

							itemMap.PutBoolean ("IsFavorite", favorites.Contains (station.Id));

							stationMap.Add (itemMap);
						}
						map.PutDataMapArrayList ("Stations", stationMap);
						map.PutLong ("UpdatedAt", DateTime.UtcNow.Ticks);

						WearableClass.DataApi.PutDataItem (client, request.AsPutDataRequest ());
					} else {
						var uri = new Uri ("wear://watch" + path);
						var query = uri.GetComponents (UriComponents.Query, UriFormat.Unescaped);
						var parts = uri.GetComponents (UriComponents.Path, UriFormat.Unescaped).Split ('/');

						var action = parts[parts.Length - 2];
						var id = int.Parse (parts.Last ());

						if (action == FavoriteAction) {
							var favorites = FavoriteManager.Obtain (this);
							handler.Post (() => {
								if (query == "add")
									favorites.AddToFavorite (id);
								else
									favorites.RemoveFromFavorite (id);
							});
						}
					}
				} finally {
					client.Disconnect ();
				}
			} catch (Exception e) {
				Android.Util.Log.Error ("WearIntegration", e.ToString ());
				AnalyticsHelper.LogException ("WearIntegration", e);
			}
		}

		async Task<Asset> CreateWearAssetFrom (Station station)
		{
			var cacheDir = CacheDir.AbsolutePath;
			var file = Path.Combine (cacheDir, "StationBackgrounds", station.Id.ToString ());
			byte[] content = null;
			var url = GoogleApis.MakeStreetViewUrl (station.Location, 640, 400);

			try {
				if (!File.Exists (file)) {
					if (client == null)
						client = new HttpClient ();
					content = await client.GetByteArrayAsync (url);
					Directory.CreateDirectory (Path.GetDirectoryName (file));
					File.WriteAllBytes (file, content);
				} else {
					content = File.ReadAllBytes (file);
				}
			} catch (Exception e) {
				e.Data ["URL"] = url;
				Android.Util.Log.Error ("StreetViewBackground", e.ToString ());
				AnalyticsHelper.LogException ("StreetViewBackground", e);
			}

			return content == null ? null : Asset.CreateFromBytes (content);
		}
	}
}

