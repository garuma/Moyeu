using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;

namespace Moyeu
{
	public struct GeoPoint
	{
		public double Lat { get; set; }
		public double Lon { get; set; }
	}

	public struct Station
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public GeoPoint Location { get; set; }
		public bool Installed { get; set; }
		public bool Locked { get; set; }
		public bool Temporary { get; set; }
		public bool Public { get; set; }
		public int BikeCount { get; set; }
		public int EmptySlotCount { get; set; }
		public int Capacity { get { return BikeCount + EmptySlotCount; } }
		//public DateTime LastUpdateTime { get; set; }

		public override bool Equals (object obj)
		{
			return obj is Station && Equals ((Station)obj);
		}

		public bool Equals (Station other)
		{
			return other.Id == Id;
		}

		public override int GetHashCode ()
		{
			return Id;
		}

		public string GeoUrl {
			get {
				var pos = Location.Lat + "," + Location.Lon;
				var location = "geo:" + pos + "?q=" + pos + "(" + Name.Replace (' ', '+') + ")";
				return location;
			}
		}
	}

	public class Hubway : IObservable<Station[]>
	{
		const string HubwayApiEndpoint = "http://thehubway.com/data/stations/bikeStations.xml";

		public static readonly Func<Station, bool> AvailableBikeStationPredicate = s => s.BikeCount > 1 && s.EmptySlotCount > 1;

		HttpClient client = new HttpClient ();
		TimeSpan freshnessTimeout;
		string savedData;

		List<HubwaySubscriber> subscribers = new List<HubwaySubscriber> ();

		public static readonly Hubway Instance = new Hubway ();

		public Hubway () : this (TimeSpan.FromMinutes (5))
		{

		}

		public Hubway (TimeSpan freshnessTimeout)
		{
			this.freshnessTimeout = freshnessTimeout;
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
				return savedData != null && DateTime.Now < (LastUpdateTime + freshnessTimeout);
			}
		}

		public async Task<Station[]> GetStations (bool forceRefresh = false, Action<string> dataCacher = null)
		{
			string data = null;

			if (HasCachedData && !forceRefresh)
				data = savedData;
			else {
				while (data == null) {
					try {
						data = await client.GetStringAsync (HubwayApiEndpoint).ConfigureAwait (false);
					} catch (Exception e) {
						Android.Util.Log.Error ("HubwayDownloader", e.ToString ());
					}
					if (data == null)
						await Task.Delay (500);
				}
			}

			if (dataCacher != null)
				dataCacher (data);

			var stations = ParseStationsFromXml (data);
			if (subscribers.Any ())
				foreach (var sub in subscribers)
					sub.Observer.OnNext (stations);
			return stations;
		}

		public Station[] ParseStationsFromXml (string data)
		{
			var doc = XDocument.Parse (data);

			savedData = data;
			LastUpdateTime = FromUnixTime (long.Parse (((string)doc.Root.Attribute ("lastUpdate"))));

			var stations = doc.Root.Elements ("station")
				.Where (station => !string.IsNullOrEmpty (station.Element ("latestUpdateTime").Value))
				.Select (station => new Station {
					Id = int.Parse (station.Element ("id").Value),
					Name = station.Element ("name").Value,
					Location = new GeoPoint {
						Lat = double.Parse (station.Element ("lat").Value),
						Lon = double.Parse (station.Element ("long").Value)
					},
					Installed = station.Element ("installed").Value == "true",
					Locked = station.Element ("locked").Value == "true",
					Temporary = station.Element ("temporary").Value == "true",
					Public = station.Element ("public").Value == "true",
					BikeCount = int.Parse (station.Element ("nbBikes").Value),
					EmptySlotCount = int.Parse (station.Element ("nbEmptyDocks").Value),
					//LastUpdateTime = FromUnixTime (long.Parse (station.Element ("latestUpdateTime").Value)),
				})
				.ToArray ();
			LastStations = stations;
			return stations;
		}

		DateTime FromUnixTime (long secs)
		{
			return (new DateTime (1970, 1, 1, 0, 0, 1, DateTimeKind.Utc) + TimeSpan.FromSeconds (secs / 1000.0)).ToLocalTime ();
		}

		public static string CutStationName (string rawStationName)
		{
			string dummy;
			return CutStationName (rawStationName, out dummy);
		}

		public static string CutStationName (string rawStationName, out string secondPart)
		{
			secondPart = string.Empty;
			var nameParts = rawStationName.Split (new string[] { "-", " at " , "/ ", " / " }, StringSplitOptions.RemoveEmptyEntries);
			if (nameParts.Length > 1)
				secondPart = string.Join (", ", nameParts.Skip (1)).Trim ();
			else
				secondPart = nameParts [0].Trim ();
			return nameParts [0].Trim ();
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

