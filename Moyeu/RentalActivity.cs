using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml.Serialization;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Graphics;

namespace Moyeu
{
	[Activity (Label = "Rentals History",
	           Theme = "@android:style/Theme.Holo.Light.DarkActionBar",
	           ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
	           LaunchMode = LaunchMode.SingleTop)]	
	public class RentalActivity : Activity
	{
		CookieContainer cookies;

		HubwayRentals rentals;
		RentalsAdapter adapter;

		ListFragment listFragment;

		int currentPageId;
		bool hasNextPage;
		bool listInitialized;
		bool loading;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.RentalActivityLayout);
			ActionBar.SetDisplayOptions (ActionBarDisplayOptions.HomeAsUp,
			                             ActionBarDisplayOptions.HomeAsUp);
			ActionBar.SetHomeButtonEnabled (true);
			listFragment = (ListFragment)FragmentManager.FindFragmentById (Resource.Id.list);
		}

		protected override void OnStart ()
		{
			base.OnStart ();
			if (currentPageId == 0)
				DoFetch ();
			if (!listInitialized) {
				listFragment.ListView.Scroll += HandleScroll;
				listInitialized = true;
			}
		}

		void HandleScroll (object sender, AbsListView.ScrollEventArgs e)
		{
			if (loading
			    || e.FirstVisibleItem + e.VisibleItemCount < e.TotalItemCount
			    || !hasNextPage)
				return;
			loading = true;
			DoFetch ();
		}

		async void DoFetch (bool forceRefresh = false)
		{
			LoginDialogFragment dialog = null;
			bool hadError = false;
			do {
				if (dialog != null)
					dialog.AdvertiseLoginError ();
				var credentials = StoredCredentials;
				if (dialog == null &&
				    (string.IsNullOrEmpty (credentials.Username) ||
				    string.IsNullOrEmpty (credentials.Password))) {
					dialog = new LoginDialogFragment ();
					dialog.Dismissed += (sender, e) => {
						if (string.IsNullOrEmpty (credentials.Username)
						    || string.IsNullOrEmpty (credentials.Password)
						    || hadError)
							Finish ();
					};
					dialog.Show (FragmentManager, "loginDialog");
				}
				if (dialog != null)
					StoredCredentials = credentials = await dialog.GetCredentialsAsync ();
				rentals = new HubwayRentals (StoredCookies, credentials);
				hadError = !(await GetRentals (forceRefresh));
				if (hadError)
					StoredCredentials = new RentalCrendentials ();
			} while (hadError);
			if (dialog != null)
				dialog.Dismiss ();
		}

		protected override void OnPause ()
		{
			base.OnPause ();
			var serializer = new XmlSerializer (typeof(CookieContainer));
			var prefs = GetPreferences (FileCreationMode.Private);
			var editor = prefs.Edit ();
			if (cookies != null && cookies.Count > 0) {
				var buffer = new System.IO.StringWriter ();
				serializer.Serialize (buffer, cookies);
				editor.PutString ("cookies", buffer.ToString ());
			}

			editor.Commit ();
		}

		public override bool OnCreateOptionsMenu (IMenu menu)
		{
			var inflater = MenuInflater;
			inflater.Inflate (Resource.Menu.rentals_menu, menu);
			return true;
		}

		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			if (item.ItemId == Resource.Id.menu_refresh) {
				currentPageId = 0;
				listFragment.SetListShownNoAnimation (false);
				listFragment.ListAdapter = null;
				DoFetch (forceRefresh: true);
				return true;
			} else if (item.ItemId == Android.Resource.Id.Home) {
				Finish ();
				return true;
			} else
				return base.OnOptionsItemSelected (item);
		}

		async Task<bool> GetRentals (bool forceRefresh = false)
		{
			loading = true;
			bool isFirst = currentPageId == 0;
			var newRentals = await rentals.GetRentals (currentPageId++);
			hasNextPage = newRentals != null && newRentals.Last ().Id != 1;
			if (newRentals == null) {
				loading = false;
				return false;
			}
			if (adapter == null || forceRefresh) {
				adapter = new RentalsAdapter (this);
				listFragment.ListAdapter = adapter;
			}
			adapter.AppendRentals (newRentals);
			if (!isFirst)
				listFragment.ListView.SmoothScrollByOffset (1);
			loading = false;
			return true;
		}

		CookieContainer StoredCookies {
			get {
				if (cookies == null) {
					var prefs = GetPreferences (FileCreationMode.Private);
					var serialized = prefs.GetString ("cookies", null);
					if (serialized != null) {
						var serializer = new XmlSerializer (typeof(CookieContainer));
						cookies = (CookieContainer)serializer.Deserialize (new System.IO.StringReader (serialized));
					}
				}
				return cookies ?? (cookies = new CookieContainer ());
			}
		}

		RentalCrendentials StoredCredentials {
			get {
				var prefs = GetPreferences (FileCreationMode.Private);
				return new RentalCrendentials {
					Username = prefs.GetString ("hubwayUsername", null),
					Password = prefs.GetString ("hubwayPass", null),
				};
			}
			set {
				var prefs = GetPreferences (FileCreationMode.Private);
				var editor = prefs.Edit ();
				editor.PutString ("hubwayUsername", value.Username ?? string.Empty);
				editor.PutString ("hubwayPass", value.Password ?? string.Empty);
				editor.Commit ();
			}
		}

		class RentalsAdapter : BaseAdapter
		{
			List<Rental?> rentals = new List<Rental?> ();
			Context context;

			Color basePriceColor = Color.Rgb (0x99, 0xcc, 0x00);
			Color endPriceColor = Color.Rgb (0xff, 0x44, 0x44);

			public RentalsAdapter (Context context)
			{
				this.context = context;
			}

			public void AppendRentals (IEnumerable<Rental> newRentals)
			{
				var lastDate = rentals.Count == 0 ? DateTime.Now : rentals.Last ().Value.DepartureTime.Date;
				foreach (var r in newRentals) {
					if (rentals.Count == 0 || r.DepartureTime.Date != lastDate) {
						rentals.Add (null);
						lastDate = r.DepartureTime.Date;
					}
					rentals.Add (r);
				}
				NotifyDataSetChanged ();
			}

			public override int Count {
				get {
					return rentals == null ? 0 : rentals.Count;
				}
			}

			public override bool AreAllItemsEnabled ()
			{
				return true;
			}

			public override bool IsEnabled (int position)
			{
				return false;
			}

			public override bool HasStableIds {
				get {
					return true;
				}
			}

			public override long GetItemId (int position)
			{
				var r = rentals [position];
				if (r == null)
					return rentals [position + 1].Value.Id << 32;
				else
					return r.Value.Id;
			}

			public override View GetView (int position, View convertView, ViewGroup parent)
			{
				var r = rentals [position];

				if (r == null) {
					var header = convertView as TextView;
					if (header == null)
						header = MakeHeaderView ();
					header.Text = rentals [position + 1].Value.DepartureTime.Date.ToLongDateString ();
					return header;
				} else {
					var view = convertView as RentalView;
					if (view == null)
						view = new RentalView (context);
					var stationFromText = view.FindViewById<TextView> (Resource.Id.rentalFromStation);
					var stationToText = view.FindViewById<TextView> (Resource.Id.rentalToStation);
					var priceText = view.FindViewById<TextView> (Resource.Id.rentalPrice);
					var chronometer = view.FindViewById<ChronometerView> (Resource.Id.rentalTime);
					var timePrimary = view.FindViewById<TextView> (Resource.Id.rentalTimePrimary);
					var timeSecondary = view.FindViewById<TextView> (Resource.Id.rentalTimeSecondary);

					var rental = r.Value;
					stationFromText.Text = Hubway.CutStationName (rental.FromStationName);
					stationToText.Text = Hubway.CutStationName (rental.ToStationName);
					if (rental.Duration > TimeSpan.FromHours (1)) {
						timePrimary.Text = rental.Duration.Hours.ToString ("D2") + " hrs";
						timeSecondary.Text = rental.Duration.Minutes.ToString ("D2") + " min";
					} else {
						timePrimary.Text = rental.Duration.Minutes.ToString ("D2") + " min";
						timeSecondary.Text = rental.Duration.Seconds.ToString ("D2") + " sec";
					}
					priceText.SetTextColor (InterpolateColor (rental.Price / 100, basePriceColor, endPriceColor));
					priceText.Text = rental.Price.ToString ("F2");
					chronometer.Time = rental.Duration;

					return view;
				}
			}

			public override int GetItemViewType (int position)
			{
				return rentals [position] == null ? 0 : 1;
			}

			public override int ViewTypeCount {
				get {
					return 2;
				}
			}

			public override Java.Lang.Object GetItem (int position)
			{
				return new Java.Lang.String (rentals [position] == null ? "header" : "element");
			}

			TextView MakeHeaderView ()
			{
				var result = new TextView (context) {
					Gravity = GravityFlags.Center,
					TextSize = 10,
				};
				result.SetPadding (4, 4, 4, 4);
				result.SetBackgroundColor (Color.Rgb (0x22, 0x22, 0x22));
				result.SetAllCaps (true);
				result.SetTypeface (Typeface.DefaultBold, TypefaceStyle.Bold);
				result.SetTextColor (Color.White);

				return result;
			}

			Color InterpolateColor (double ratio, Color startColor, Color endColor)
			{
				return Color.Rgb ((int)((endColor.R - startColor.R) * ratio + startColor.R),
				                  (int)((endColor.G - startColor.G) * ratio + startColor.G),
				                  (int)((endColor.B - startColor.B) * ratio + startColor.B));
			}
		}

		class RentalView : FrameLayout
		{
			public RentalView (Context context) : base (context)
			{
				var inflater = Context.GetSystemService (Context.LayoutInflaterService).JavaCast<LayoutInflater> ();
				inflater.Inflate (Resource.Layout.RentalItem, this, true);
			}

			public Rental Rental {
				get;
				set;
			}
		}
	}
}

