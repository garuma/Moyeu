
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

		public override void OnCreate ()
		{
			base.OnCreate ();
			Android.Util.Log.Error ("WearIntegration", "Created");
		}

		public override void OnMessageReceived (IMessageEvent messageEvent)
		{
			base.OnMessageReceived (messageEvent);
			if (!messageEvent.Path.StartsWith (SearchStationPath))
				return;

			HandleMessage (messageEvent);
		}

		async void HandleMessage (IMessageEvent message)
		{
			try {
				Android.Util.Log.Info ("WearIntegration", "Received Message");
				var client = new GoogleApiClientBuilder (this)
					.AddApi (WearableClass.Api)
					.AddApi (LocationServices.Api)
					.Build ();

				var result = client.BlockingConnect (30, Java.Util.Concurrent.TimeUnit.Seconds);
				if (!result.IsSuccess)
					return;

				try {
					var lastLocation = LocationServices.FusedLocationApi.GetLastLocation (client);
					if (lastLocation == null)
						return;

					var stations = Hubway.Instance.LastStations;
					if (stations == null)
						stations = await Hubway.Instance.GetStations ();

					var currentPoint = new GeoPoint {
						Lat = lastLocation.Latitude,
						Lon = lastLocation.Longitude
					};
					var nearestStations = Hubway.GetStationsAround (stations, currentPoint);

					using (var stream = new System.IO.MemoryStream ()) {
						GeoUtils.DumpLocation (currentPoint, stream);
						StationUtils.DumpStations (nearestStations, stream);
						var bytes = stream.ToArray ();
						WearableClass.MessageApi.SendMessage (client,
						                                      message.SourceNodeId,
						                                      SearchStationPath + "/Answer",
						                                      bytes).Await ();
					}
				} finally {
					client.Disconnect ();
				}
			} catch (Exception e) {
				Android.Util.Log.Error ("WearIntegration", e.ToString ());
			}
		}
	}
}

