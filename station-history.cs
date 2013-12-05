using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

class T
{
	const int NumDataPoint = 6;

	public static void Main (string[] args)
	{
		var basePath = args[0];
		var baseOutPath  = args[1];
		var currentTime = DateTime.UtcNow;
		// Round off time if not on a 5 minutes base
		if ((currentTime.Minute % 5) != 0) {
			var lastMinute = currentTime.Minute % 10;
			currentTime -= TimeSpan.FromMinutes (lastMinute - (lastMinute > 5 ? 5 : 0));
		}
		
		// station ID -> amount of bikes at that time
		var dataPoints = new Dictionary<string, List<int>> ();
		var times = new List<DateTime> ();
		
		for (int i = 0; i < NumDataPoint; i++) {
			var time = currentTime - TimeSpan.FromMinutes (i * 5);
			var path = GetFilePathForDate (basePath, time);
			GatherDataPointsFromFile (path, dataPoints);
			times.Add (time);
		}

		var output = Path.Combine (baseOutPath, "history");
		var lines = new List<string> ();
		lines.Add (string.Join ("|", times.Select (t => t.ToBinary ().ToString ()).Reverse ()));
		lines.AddRange (dataPoints.Select (dp => dp.Key + "|" + string.Join ("|", dp.Value.Select (v => v.ToString ()).Reverse ())));
		File.WriteAllLines (output, lines);

		Console.WriteLine ("Success");
	}

	static string GetFilePathForDate (string basePath, DateTime date)
	{
		var baseName = string.Format ("bikeStations_{0:yyyy-MM-dd_HH-mm}-", date);
		return Enumerable.Range (0, 20)
			.Select (i => Path.Combine (basePath, baseName + i.ToString ("D2") + ".xml"))
			.First (p => File.Exists (p));
	}

	static void GatherDataPointsFromFile (string filepath, Dictionary<string, List<int>> dataPoints)
	{
		using (var fd = File.OpenRead (filepath)) {
			var doc = XDocument.Load (fd);
			foreach (var station in doc.Root.Elements ("station")) {
				var name = (string)station.Element ("id");
				List<int> list;
				if (!dataPoints.TryGetValue (name, out list))
					dataPoints[name] = list = new List<int> (NumDataPoint);
				list.Add (int.Parse ((string)station.Element ("nbBikes")));
			}
		}
	}
}