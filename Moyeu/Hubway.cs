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
		public DateTime LastUpdateTime { get; set; }

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

	public class Hubway
	{
		const string HubwayApiEndpoint = "http://thehubway.com/data/stations/bikeStations.xml";

		HttpClient client = new HttpClient ();
		TimeSpan freshnessTimeout;
		string savedData;

		public static readonly Hubway Instance = new Hubway ();

		public Hubway () : this (TimeSpan.FromMinutes (10))
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

		public Station[] GetClosestStationTo (Station[] stations, params GeoPoint[] locations)
		{
			var distanceToGeoPoints = new SortedDictionary<double, Station>[locations.Length];
			foreach (var station in stations.Where (s => s.BikeCount > 1 && s.EmptySlotCount > 1)) {
				for (int i = 0; i < locations.Length; i++) {
					if (distanceToGeoPoints [i] == null)
						distanceToGeoPoints [i] = new SortedDictionary<double, Station> ();
					distanceToGeoPoints [i].Add (GeoUtils.Distance (locations[i], station.Location), station);
				}
			}

			return distanceToGeoPoints.Select (ds => ds.First ().Value).ToArray ();
		}

		public async Task<Station[]> GetStations (bool forceRefresh = false)
		{
			string data = null;

			if (savedData != null && DateTime.Now < (LastUpdateTime + freshnessTimeout) && !forceRefresh)
				data = savedData;
			else {
				while (data == null) {
					try {
						data = await client.GetStringAsync (HubwayApiEndpoint).ConfigureAwait (false);
					} catch (Exception e) {
						Android.Util.Log.Error ("HubwayDownloader", e.ToString ());
					}
					if (data == null)
						await Task.Delay (100);
				}
			}

			var doc = XDocument.Parse (data);

			savedData = data;
			LastUpdateTime = FromUnixTime (long.Parse (((string)doc.Root.Attribute ("lastUpdate"))));

			var stations = doc.Root.Elements ("station")
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
					LastUpdateTime = FromUnixTime (long.Parse (station.Element ("latestUpdateTime").Value)),
				})
				.ToArray ();
			LastStations = stations;
			return stations;
		}

		DateTime FromUnixTime (long secs)
		{
			return (new DateTime (1970, 1, 1, 0, 0, 1, DateTimeKind.Utc) + TimeSpan.FromSeconds (secs / 1000.0)).ToLocalTime ();
		}
	}
}

