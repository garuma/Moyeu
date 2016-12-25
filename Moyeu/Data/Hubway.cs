using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;

namespace Moyeu
{
	using GbfsStationInformationResponse = GbfsResponse<GbfsStationInformationData>;
	using GbfsStationStatusResponse = GbfsResponse<GbfsStationStatusData>;

	public class Hubway : IObservable<Station[]>
	{
		const string HubwayStationInfoUrl = "https://gbfs.thehubway.com/gbfs/en/station_information.json";
		const string HubwayStationStatusUrl = "https://gbfs.thehubway.com/gbfs/en/station_status.json";

		public static readonly Func<Station, bool> AvailableBikeStationPredicate = s => s.BikeCount > 1 && s.EmptySlotCount > 1;

		HttpClient client;
		JsonSerializer serializer;
		TimeSpan freshnessTimeout;

		List<HubwaySubscriber> subscribers = new List<HubwaySubscriber> ();
		Dictionary<int, GbfsStationInformation> stationDescriptions;

		public static readonly Hubway Instance = new Hubway ();

		public Hubway () : this (TimeSpan.FromMinutes (5))
		{

		}

		public Hubway (TimeSpan freshnessTimeout)
		{
			this.freshnessTimeout = freshnessTimeout;
			this.client = new HttpClient (new Xamarin.Android.Net.AndroidClientHandler ());
			this.serializer = JsonSerializer.CreateDefault ();
		}

		public DateTime LastUpdateTime {
			get;
			private set;
		}

		public Station[] LastStations {
			get;
			private set;
		}

		public static Station[] GetStationsAround (Station[] stations, GeoPoint location, double minDistance = 100, int maxItems = 4)
		{
			var dic = new SortedDictionary<double, Station> ();
			foreach (var s in stations) {
				var d = GeoUtils.Distance (location, s.Location);
				if (d < minDistance)
					dic.Add (d, s);
			}
			return dic.Select (ds => ds.Value).Take (maxItems).ToArray ();
		}

		public Station[] GetClosestStationTo (Station[] stations, params GeoPoint[] locations)
		{
			return GetClosestStationTo (stations, null, locations);
		}

		public Station[] GetClosestStationTo (Station[] stations, Func<Station, bool> filter, params GeoPoint[] locations)
		{
			var distanceToGeoPoints = new SortedDictionary<double, Station>[locations.Length];
			var ss = filter == null ? (IEnumerable<Station>)stations : stations.Where (filter);
			foreach (var station in ss) {
				for (int i = 0; i < locations.Length; i++) {
					if (distanceToGeoPoints [i] == null)
						distanceToGeoPoints [i] = new SortedDictionary<double, Station> ();
					distanceToGeoPoints [i].Add (GeoUtils.Distance (locations[i], station.Location), station);
				}
			}

			return distanceToGeoPoints.Select (ds => ds.First ().Value).ToArray ();
		}

		public bool HasCachedData {
			get {
				return LastStations != null && DateTime.UtcNow < (LastUpdateTime + freshnessTimeout);
			}
		}

		public async Task<Station[]> GetStations (bool forceRefresh = false, Action<string> dataCacher = null)
		{
			string data = null;

			if (HasCachedData && !forceRefresh)
				return LastStations;

			if (stationDescriptions == null)
				stationDescriptions = await GetStationDescriptions ().ConfigureAwait (false);

			while (data == null) {
				try {
					data = await client.GetStringAsync (HubwayStationStatusUrl).ConfigureAwait (false);
				} catch (Exception e) {
					AnalyticsHelper.LogException ("HubwayDownloader", e);
					Android.Util.Log.Error ("HubwayDownloader", e.ToString ());
				}
				if (data == null)
					await Task.Delay (500);
			}

			if (dataCacher != null)
				dataCacher (data);

			var stations = ParseStationsFromContent (data);
			if (subscribers.Any ())
				foreach (var sub in subscribers)
					sub.Observer.OnNext (stations);
			return stations;
		}

		public Station[] ParseStationsFromContent (string data)
		{
			using (var reader = new JsonTextReader (new StringReader (data))) {
				var response = serializer.Deserialize<GbfsStationStatusResponse> (reader);
				LastUpdateTime = response.LastUpdated;

				var stations = new List<Station> (response.Data.Stations.Length);
				foreach (var gbfsStation in response.Data.Stations) {
					GbfsStationInformation desc;
					if (!stationDescriptions.TryGetValue (gbfsStation.Id, out desc))
						continue;
					var station = new Station {
						Id = gbfsStation.Id,
						Name = desc.Name,
						Location = new GeoPoint {
							Lat = desc.Latitude,
							Lon = desc.Longitude
						},
						Installed = gbfsStation.IsInstalled,
						Locked = !gbfsStation.IsRenting || !gbfsStation.IsReturning,
						BikeCount = gbfsStation.BikesAvailable,
						EmptySlotCount = gbfsStation.DocksAvailable,
						LastUpdateTime = gbfsStation.LastReported
					};
					stations.Add (station);
				}
				var newStations = stations.ToArray ();
				LastStations = newStations;
				return newStations;
			}
		}

		async Task<Dictionary<int, GbfsStationInformation>> GetStationDescriptions ()
		{
			var content = await client.GetStringAsync (HubwayStationInfoUrl).ConfigureAwait (false);
			using (var reader = new JsonTextReader (new StringReader (content))) {
				var response = serializer.Deserialize<GbfsStationInformationResponse> (reader);
				return response.Data.Stations.ToDictionary (s => s.Id, s => s);
			}
		}

		DateTime FromUnixTime (long secs)
		{
			return (new DateTime (1970, 1, 1, 0, 0, 1, DateTimeKind.Utc) + TimeSpan.FromSeconds (secs / 1000.0)).ToLocalTime ();
		}

		public IDisposable Subscribe (IObserver<Station[]> observer)
		{
			var sub = new HubwaySubscriber (subscribers.Remove, observer);
			subscribers.Add (sub);
			return sub;
		}

		class HubwaySubscriber : IDisposable
		{
			Func<HubwaySubscriber, bool> unsubscribe;

			public HubwaySubscriber (Func<HubwaySubscriber, bool> unsubscribe, IObserver<Station[]> observer)
			{
				Observer = observer;
				this.unsubscribe = unsubscribe;
			}

			public IObserver<Station[]> Observer {
				get;
				set;
			}

			public void Dispose ()
			{
				unsubscribe (this);
			}
		}
	}
}

