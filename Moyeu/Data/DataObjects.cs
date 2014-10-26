using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;

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

	public static class StationUtils
	{
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

		public static void DumpStations (IList<Station> stations, Stream stream)
		{
			using (var writer = new BinaryWriter (stream, System.Text.Encoding.UTF8, true)) {
				writer.Write (stations.Count);
				foreach (var s in stations) {
					writer.Write (s.Id);
					writer.Write (s.Name);
					writer.Write (s.Location.Lat);
					writer.Write (s.Location.Lon);
					writer.Write (s.Installed);
					writer.Write (s.Locked);
					writer.Write (s.Temporary);
					writer.Write (s.Public);
					writer.Write (s.BikeCount);
					writer.Write (s.EmptySlotCount);
				}
			}
		}

		public static IList<Station> ParseStations (Stream stream)
		{
			using (var reader = new BinaryReader (stream, System.Text.Encoding.UTF8, true)) {
				var count = reader.ReadInt32 ();
				var stations = new Station[count];
				for (int i = 0; i < count; i++) {
					stations [i] = new Station {
						Id = reader.ReadInt32 (),
						Name = reader.ReadString (),
						Location = new GeoPoint { Lat = reader.ReadDouble (), Lon = reader.ReadDouble () },
						Installed = reader.ReadBoolean (),
						Locked = reader.ReadBoolean (),
						Temporary = reader.ReadBoolean (),
						Public = reader.ReadBoolean (),
						BikeCount = reader.ReadInt32 (),
						EmptySlotCount = reader.ReadInt32 ()
					};
				}

				return stations;
			}
		}

		public static void DumpFavorites (HashSet<int> favorites, Stream stream)
		{
			using (var writer = new BinaryWriter (stream, System.Text.Encoding.UTF8, true)) {
				writer.Write (favorites.Count);
				foreach (var fav in favorites)
					writer.Write (fav);
			}
		}

		public static HashSet<int> ParseFavorites (Stream stream)
		{
			using (var reader = new BinaryReader (stream, System.Text.Encoding.UTF8, true)) {
				var count = reader.ReadInt32 ();
				return new HashSet<int> (
					Enumerable.Range (0, count).Select (i => reader.ReadInt32 ())
				);
			}
		}
	}
}

