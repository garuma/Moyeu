using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Linq;

using HtmlParserSharp;

using Log = Android.Util.Log;

namespace Moyeu
{
	[Serializable]
	public class RentalCrendentials {
		public string Username { get; set; }
		public string Password { get; set; }
		public string UserId { get; set; }
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
		const string HubwayLoginUrl = "https://secure.thehubway.com/profile/login";
		const string HubwayLoginCheckUrl = "https://secure.thehubway.com/profile/login_check";
		const string HubwayProfileUrl = "https://secure.thehubway.com/profile/";
		const string HubwayRentalsUrl = "https://secure.thehubway.com/profile/trips/";

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
			const int ResultsPerPage = 10;
			bool needsAuth = false;

			for (int i = 0; i < 4; i++) {
				try {
					if (needsAuth) {
						if (await LoginToHubway ().ConfigureAwait (false))
							needsAuth = false;
						else
							continue;
					}

					if (string.IsNullOrEmpty (credentials.UserId)) {
						credentials.UserId = await GetHubwayUserId ().ConfigureAwait (false);
						if (string.IsNullOrEmpty (credentials.UserId)) {
							needsAuth = true;
							continue;
						}
					}

					var rentalsUrl = HubwayRentalsUrl + credentials.UserId;
					if (page > 0)
						rentalsUrl += "?pageNumber=" + page;
					var answer = await Client.GetStringAsync (rentalsUrl).ConfigureAwait (false);

					var parser = new SimpleHtmlParser ();
					var doc = parser.ParseString (answer);
					var div = doc.GetElementsByTagName ("section")
						.OfType<XmlElement> ()
						.First (s => s.GetAttribute ("class") == "ed-profile-page__content");
					var rows = div.GetElementsByTagName ("div")
						.OfType<XmlElement> ()
						.Where (n => n.ContainsClass ("ed-table__item_trip"));
					return rows.Select (row => {
						var cells = row.GetElementsByTagName ("div").OfType<XmlElement> ().ToList ();
						/* 0 <div>
						 * 1   <div>time start
						 * 2   <div>station start
						 *   </div>
						 * 3 <div>
						 * 4   <div>time end
						 * 5   <div>station end
						 *   </div>
						 * 6 <div>duration
						 * 7 <div>billed
						 */
						var rental = new Rental {
							FromStationName = cells[2].InnerText.Trim (),
							ToStationName = cells[5].InnerText.Trim (),
							Duration = ParseRentalDuration (cells[6].InnerText.Trim ()),
							Price = ParseRentalPrice (cells[7].InnerText.Trim ()),
							DepartureTime = DateTime.Parse (cells[1].InnerText,
							                                System.Globalization.CultureInfo.InvariantCulture),
							ArrivalTime = DateTime.Parse (cells[4].InnerText,
							                              System.Globalization.CultureInfo.InvariantCulture)
						};
						rental.Id  = ((long)rental.DepartureTime.GetHashCode ()) << 32;
						rental.Id |= (uint)rental.ArrivalTime.GetHashCode ();
						return rental;
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

		/* This is a two steps process:
		 *  1st: we need to visit the login form page to get two important pieces of data
		 *    - the session ID cookie which will stay in our cache
		 *    - the CSRF token that is generated on the fly in the form
		 *  2nd: armed with those two things, we can then post to the login check page which
		 *  will simply stamp our session ID as valid
		 */
		async Task<bool> LoginToHubway ()
		{
			var loginPage = await Client.GetStringAsync (HubwayLoginUrl).ConfigureAwait (false);
			var parser = new SimpleHtmlParser ();
			var doc = parser.ParseString (loginPage);
			var form = doc.GetElementsByTagName ("form")
				.OfType<XmlElement> ()
				.FirstOrDefault (n => n.GetAttribute ("class") == "ed-popup-form_login__form");
			var inputs = form.GetElementsByTagName ("input").OfType<XmlElement> (). ToList ();
			var csrfToken = inputs
				.OfType<XmlElement> ()
				.First (n => n.GetAttribute ("name") == "_login_csrf_security_token")
				.GetAttribute ("value");

			var content = new FormUrlEncodedContent (new Dictionary<string, string> {
				{ "_username", credentials.Username },
				{ "_password", credentials.Password },
				{ "_failure_path", "eightd_bike_profile__login" },
				{ "ed_from_login_popup", "true" },
				{ "_login_csrf_security_token", csrfToken }
			});
			var login = await Client.PostAsync (HubwayLoginCheckUrl, content).ConfigureAwait (false);
			return login.StatusCode == HttpStatusCode.Found && login.Headers.Location == new Uri (HubwayProfileUrl);
		}

		async Task<string> GetHubwayUserId ()
		{
			// We have to visit the profile page so that we can get the
			// right rentals link containing the user ID
			const string BaseTripUrl = "/profile/trips/";
			var loginPage = await Client.GetStringAsync (HubwayProfileUrl).ConfigureAwait (false);
			var urlIndex = loginPage.IndexOf (BaseTripUrl);
			return loginPage.Substring (urlIndex + BaseTripUrl.Length, 10);
		}

		TimeSpan ParseRentalDuration (string duration)
		{
			TimeSpan result = TimeSpan.Zero;
			var components = duration.Split (' ')
				.Where ((e, i) => (i % 2) == 0)
				.Select (int.Parse);
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
			return price.Substring (1).ToSafeDouble ();
		}
	}

	public static class HtmlNodeExtensions
	{
		public static bool ContainsClass (this XmlElement node, string className)
		{
			var cls = node.GetAttribute ("class");
			return cls != null && cls.Contains (className);
		}
	}
}

