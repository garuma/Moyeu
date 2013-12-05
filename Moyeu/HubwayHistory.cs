using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;

namespace Moyeu
{
	public class HubwayHistory
	{
		const string HistoryApiEndpoint = "http://api.moyeuapp.net/history";

		HttpClient client = new HttpClient ();
		Dictionary<int, IEnumerable<KeyValuePair<DateTime, int>>> historyCache;
		DateTime lastFetch = DateTime.MinValue;

		public async Task<IEnumerable<KeyValuePair<DateTime, int>>> GetStationHistory (int stationID)
		{
			if (NeedFetching)
				await FetchData ();

			IEnumerable<KeyValuePair<DateTime, int>> result = null;
			if (!historyCache.TryGetValue (stationID, out result))
			    return Enumerable.Empty<KeyValuePair<DateTime, int>> ();
			return result;
		}

		bool NeedFetching {
			get {
				var now = DateTime.UtcNow;
				// The dumb check to begin with
				return Math.Abs ((now - lastFetch).TotalMinutes) > 5
					// If minute decimal change, it's necessarily a re-fetch
					|| lastFetch.Minute / 10 != now.Minute / 10
					// Also if the two timestamps are on each side of a 5
					|| ((lastFetch.Minute % 10) < 5 ^ (now.Minute % 10) < 5);
			}
		}

		async Task FetchData ()
		{
			historyCache = new Dictionary<int, IEnumerable<KeyValuePair<DateTime, int>>> ();

			for (int i = 0; i < 3; i++) {
				try {
					var data = await client.GetStringAsync (HistoryApiEndpoint);
					// If another update was done in the meantime, no need to process it
					if (!NeedFetching)
						return;

					var reader = new StringReader (data);
					string line = reader.ReadLine ();
					var times = line.Split ('|').Select (t => DateTime.FromBinary (long.Parse (t))).ToArray ();
					while ((line = reader.ReadLine ()) != null) {
						var split = line.Split ('|');
						var id = int.Parse (split[0]);
						var datapoints = split.Skip (1).Select ((v, index) => {
							var time = times[index];
							var num = int.Parse (v);

							return new KeyValuePair<DateTime, int> (time, num);
						}).ToArray ();
						historyCache[id] = datapoints;
					}
					lastFetch = DateTime.UtcNow;
				} catch (Exception e) {
					Android.Util.Log.Error ("HistoryDownloader", e.ToString ());
				}
			}
		}
	}
}

