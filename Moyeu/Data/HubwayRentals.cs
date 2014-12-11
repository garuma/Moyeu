using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;

using HtmlAgilityPack;

using Log = Android.Util.Log;

namespace Moyeu
{
	[Serializable]
	public struct RentalCrendentials {
		public string Username { get; set; }
		public string Password { get; set; }
	}

	public struct Rental {
		public long Id { get; set; }
		public string FromStationName { get; set; }
		public string ToStationName { get; set; }
		public double Price { get; set; }
		public DateTime DepartureTime { get; set; }
		public DateTime ArrivalTime { get; set; }
		public TimeSpan Duration { get; set; }
	}

	public interface IRentalStorage {
		Rental[] GetStoredRentals ();
		void StoreRentals (Rental[] rentals);
	}

	public class HubwayRentals
	{
		const string HubwayLoginUrl = "https://www.thehubway.com/login";
		const string HubwayRentalsUrl = "https://www.thehubway.com/member/rentals";

		CookieContainer cookies;
		RentalCrendentials credentials;
		HttpClient client;

		public HubwayRentals (CookieContainer cookies, RentalCrendentials credentials)
		{
			this.cookies = cookies;
			this.credentials = credentials;
		}

		HttpClient Client {
			get {
				if (client == null) {
					ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
					client = new HttpClient (new HttpClientHandler {
						AllowAutoRedirect = false,
						CookieContainer = cookies,
						UseCookies = true,
					});
				}
				return client;
			}
		}

		public async Task<Rental[]> GetRentals (int page)
		{
			bool needsAuth = false;
			var rentalsUrl = HubwayRentalsUrl;
			if (page > 0)
				rentalsUrl += "/" + (page * 20);

			for (int i = 0; i < 4; i++) {
				try {
					if (needsAuth) {
						var content = new FormUrlEncodedContent (new Dictionary<string, string> {
							{ "username", credentials.Username },
							{ "password", credentials.Password }
						});
						var login = await Client.PostAsync (HubwayLoginUrl, content).ConfigureAwait (false);
						if (login.StatusCode == HttpStatusCode.Found)
							needsAuth = false;
						else
							continue;
					}

					var answer = await Client.GetStringAsync (rentalsUrl).ConfigureAwait (false);

					var doc = new HtmlDocument ();
					doc.LoadHtml (answer);
					var div = doc.GetElementById ("content");
					var table = div.Element ("table");
					return table.Element ("tbody").Elements ("tr").Select (row => {
						var items = row.Elements ("td").ToArray ();
						return new Rental {
							Id = long.Parse (items[0].InnerText.Trim ()),
							FromStationName = items [1].InnerText.Trim (),
							ToStationName = items [3].InnerText.Trim (),
							Duration = ParseRentalDuration (items [5].InnerText.Trim ()),
							Price = ParseRentalPrice (items [6].InnerText.Trim ()),
							DepartureTime = DateTime.Parse(items[2].InnerText,
							                               System.Globalization.CultureInfo.InvariantCulture),
							ArrivalTime = DateTime.Parse(items[4].InnerText,
							                             System.Globalization.CultureInfo.InvariantCulture)
						};
					}).ToArray ();
				} catch (HttpRequestException htmlException) {
					// Super hacky but oh well
					if (!needsAuth)
						needsAuth = htmlException.Message.Contains ("302");
					continue;
				} catch (Exception e) {
					AnalyticsHelper.LogException ("RentalsGenericError", e);
					Log.Error ("RentalsGenericError", e.ToString ());
					break;
				}
			}

			return null;
		}

		TimeSpan ParseRentalDuration (string duration)
		{
			TimeSpan result = TimeSpan.Zero;
			var components = duration.Split (new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
				.Select (c => c.Split (' ').FirstOrDefault () ?? "0")
				.Select (v => int.Parse (v));
			var access = new Stack<int> (components);
			if (access.Count > 0)
				result += TimeSpan.FromSeconds (access.Pop ());
			if (access.Count > 0)
				result += TimeSpan.FromMinutes (access.Pop ());
			if (access.Count > 0)
				result += TimeSpan.FromHours (access.Pop ());
			if (access.Count > 0)
				result += TimeSpan.FromDays (access.Pop ());

			return result;
		}

		double ParseRentalPrice (string price)
		{
			return price.Substring ("$ ".Length).ToSafeDouble ();
		}
	}
}

