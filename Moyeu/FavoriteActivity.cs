using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Http;

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
	[Activity (Label = "Favorites",
	           Theme = "@android:style/Theme.Holo.Light.DarkActionBar",
	           ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
	           LaunchMode = LaunchMode.SingleTop)]
	public class FavoriteActivity : Activity
	{
		FavoriteListFragment list;
		FavoriteManager favManager;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			this.favManager = new FavoriteManager (this);
			var adapter = new FavoriteAdapter (this);
			adapter.SetStationIds (favManager.GetFavoritesStationIds ());

			SetContentView (Resource.Layout.FavoriteActivityLayout);
			list = (FavoriteListFragment)FragmentManager.FindFragmentById (Resource.Id.FavoriteListFragment);
			list.ListAdapter = adapter;
			list.ItemClick += (sender, e) => {
				SetResult ((Result)e.Id);
				Finish ();
			};
			
			ActionBar.SetDisplayOptions (ActionBarDisplayOptions.HomeAsUp,
			                             ActionBarDisplayOptions.HomeAsUp);
			ActionBar.SetHomeButtonEnabled (true);
		}

		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			if (item.ItemId == Android.Resource.Id.Home) {
				SetResult (Result.Canceled);
				Finish ();
				return true;
			} else
				return base.OnOptionsItemSelected (item);
		}
	}

	class FavoriteListFragment : ListFragment
	{
		public event EventHandler<AdapterView.ItemClickEventArgs> ItemClick;

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);
			SetEmptyText ("Add favorites by tapping\non a station info window");
			ListView.ItemClick += (sender, e) => {
				if (ItemClick != null)
					ItemClick (this, e);
			};
		}
	}

	class FavoriteAdapter : BaseAdapter
	{
		List<Station> stations;
		Context context;
		static BitmapCache cache;

		public FavoriteAdapter (Context context)
		{
			this.context = context;
			if (cache == null)
				cache = BitmapCache.CreateCache (context, "MapsPictures");
		}

		public async void SetStationIds (HashSet<int> ids)
		{
			while (true) {
				try {
					var stationList = await Hubway.Instance.GetStations ();
					stations = stationList
						.Where (s => ids.Contains (s.Id))
						.OrderBy (s => s.Name)
						.ToList ();
				} catch (Exception e) {
					Android.Util.Log.Error ("Favorites", e.ToString ());
				}
				NotifyDataSetChanged ();
				break;
			}
		}

		public override Java.Lang.Object GetItem (int position)
		{
			return new Java.Lang.String (stations[position].Name);
		}

		public override long GetItemId (int position)
		{
			return stations [position].Id;
		}

		public override View GetView (int position, View convertView, ViewGroup parent)
		{
			var view = EnsureView (convertView);
			var version = Interlocked.Increment (ref view.VersionNumber);

			var mapView = view.FindViewById<ImageView> (Resource.Id.StationMap);
			var stationName = view.FindViewById<TextView> (Resource.Id.MainStationName);
			var secondStationName = view.FindViewById<TextView> (Resource.Id.SecondStationName);
			var bikeNumber = view.FindViewById<TextView> (Resource.Id.BikeNumber);
			var slotNumber = view.FindViewById<TextView> (Resource.Id.SlotNumber);

			var station = stations [position];
			view.Station = station;

			var nameParts = station.Name.Split (new string[] { "-", " at " }, StringSplitOptions.RemoveEmptyEntries);
			stationName.Text = nameParts [0].Trim ();
			if (nameParts.Length > 1)
				secondStationName.Text = string.Join (string.Empty, nameParts.Skip (1)).Trim ();
			else
				secondStationName.Tag = string.Empty;
			bikeNumber.Text = station.BikeCount.ToString ();
			slotNumber.Text = station.Capacity.ToString ();

			string mapUrl = MakeMapUrl (station.Location);
			Bitmap mapBmp = null;
			if (cache.TryGet (mapUrl, out mapBmp))
				mapView.SetImageDrawable (new RoundCornerDrawable (mapBmp));
			else
				LoadMap (view, mapView, version, mapUrl);

			return view;
		}

		FavoriteView EnsureView (View convertView)
		{
			return (convertView as FavoriteView) ?? new FavoriteView (context);
		}

		public override int Count {
			get {
				return stations == null ? 0 : stations.Count;
			}
		}

		public override bool AreAllItemsEnabled ()
		{
			return true;
		}

		public override bool IsEnabled (int position)
		{
			return true;
		}

		async void LoadMap (FavoriteView view, ImageView mapView, long version, string mapUrl)
		{
			var client = new HttpClient ();
			try {
				var data = await client.GetByteArrayAsync (mapUrl);
				var bmp = await BitmapFactory.DecodeByteArrayAsync (data, 0, data.Length);
				cache.AddOrUpdate (mapUrl, bmp, TimeSpan.FromDays (90));
				if (view.VersionNumber == version) {
					mapView.Animate ().Alpha (0).WithEndAction (new Java.Lang.Runnable (() => {
						mapView.SetImageDrawable (new RoundCornerDrawable (bmp));
						mapView.Animate ().Alpha (1).SetDuration (250).Start ();
					})).SetDuration (150).Start ();
				}
			} catch (Exception e) {
				Android.Util.Log.Error ("MapDownloader", e.ToString ());
			}
		}

		string MakeMapUrl (GeoPoint location)
		{
			// DP
			const int Width = 65;
			const int Height = 42;

			var parameters = new Dictionary<string, string> {
				{ "key", "AIzaSyB9AJUwndABdrlBPFsd399EfCOlnXWBcYk" },
				{ "zoom", "13" },
				{ "sensor", "true" },
				{ "size", Width.ToPixels () + "x" + Height.ToPixels () },
				{ "center", location.Lat + "," + location.Lon },
				{ "markers", "size:tiny|" + location.Lat + "," + location.Lon }
			};

			return "https://maps.googleapis.com/maps/api/staticmap?"
				+ string.Join ("&", parameters.Select (pvp => pvp.Key + "=" + pvp.Value));
		}
	}

	class FavoriteView : FrameLayout
	{
		public FavoriteView (Context context) : base (context)
		{
			var inflater = Context.GetSystemService (Context.LayoutInflaterService).JavaCast<LayoutInflater> ();
			inflater.Inflate (Resource.Layout.FavoriteItem, this, true);
			var mapView = FindViewById<ImageView> (Resource.Id.StationMap);
			mapView.Focusable = false;
			mapView.FocusableInTouchMode = false;
			mapView.Click += (sender, e) => {
				var uri = Android.Net.Uri.Parse (Station.GeoUrl);
				Android.Util.Log.Info ("MapUri", uri.ToString ());
				var intent = new Intent (Intent.ActionView, uri);
				context.StartActivity (intent);
			};
		}

		public long VersionNumber;

		public Station Station {
			get;
			set;
		}
	}
}

