using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Locations;
using Android.Animation;

using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Gms.Location;
using Android.Gms.Common;
using ConnectionCallbacks = Android.Gms.Common.IGooglePlayServicesClientConnectionCallbacks;
using ConnectionFailedListener = Android.Gms.Common.IGooglePlayServicesClientOnConnectionFailedListener;

using Android.Support.V4.App;
using Android.Support.V4.Widget;

namespace Moyeu
{
	[Activity (Label = "Moyeu",
	           MainLauncher = true,
	           Theme = "@android:style/Theme.Holo.Light.DarkActionBar",
	           ScreenOrientation = ScreenOrientation.Portrait,
	           ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
	           LaunchMode = LaunchMode.SingleTop)]
	[IntentFilter (new[] { "android.intent.action.SEARCH" }, Categories = new[] { "android.intent.category.DEFAULT" })]
	[MetaData ("android.app.searchable", Resource = "@xml/searchable")]
	public class MainActivity
		: Android.Support.V4.App.FragmentActivity, IObserver<Station[]>, ConnectionCallbacks, ConnectionFailedListener
	{
		const int ConnectionFailureResolutionRequest = 9000;

		HubwayMapFragment mapFragment;
		FavoriteFragment favoriteFragment;
		RentalFragment rentalFragment;
		Android.Support.V4.App.Fragment currentFragment;

		DrawerLayout drawer;
		ActionBarDrawerToggle drawerToggle;
		ListView drawerMenu;
		ListView drawerAround;

		DrawerAroundAdapter aroundAdapter;
		LocationClient locationClient;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			AndroidExtensions.Initialize (this);
			SetContentView (Resource.Layout.Main);

			this.drawer = FindViewById<DrawerLayout> (Resource.Id.drawer_layout);
			this.drawerToggle = new MoyeuActionBarToggle (this,
			                                              drawer,
			                                              Resource.Drawable.ic_drawer,
			                                              Resource.String.open_drawer,
			                                              Resource.String.close_drawer) {
				OpenCallback = () => {
					ActionBar.Title = Title;
					currentFragment.HasOptionsMenu = false;
					InvalidateOptionsMenu ();
				},
				CloseCallback = () => {
					if (currentFragment != null) {
						ActionBar.Title = ((IMoyeuSection)currentFragment).Title;
						currentFragment.HasOptionsMenu = true;
					}
					InvalidateOptionsMenu ();
				},
			};
			drawer.SetDrawerShadow (Resource.Drawable.drawer_shadow, (int)GravityFlags.Left);
			drawer.SetDrawerListener (drawerToggle);
			ActionBar.SetDisplayHomeAsUpEnabled (true);
			ActionBar.SetHomeButtonEnabled (true);

			Hubway.Instance.Subscribe (this);
			FavoriteManager.FavoritesChanged += (sender, e) => aroundAdapter.Refresh ();

			drawerMenu = FindViewById<ListView> (Resource.Id.left_drawer);
			var header = LayoutInflater.Inflate (Resource.Layout.DrawerHeader, drawerMenu, false);
			header.FindViewById<TextView> (Resource.Id.titleText).Text = "Sections";
			drawerMenu.AddHeaderView (header, null, false);
			drawerMenu.ItemClick += HandleSectionItemClick;
			drawerMenu.Adapter = new DrawerMenuAdapter (this);

			drawerAround = FindViewById<ListView> (Resource.Id.left_drawer_around);
			header = LayoutInflater.Inflate (Resource.Layout.DrawerHeader, drawerAround, false);
			header.FindViewById<TextView> (Resource.Id.titleText).Text = "Around You";
			drawerAround.AddHeaderView (header, null, false);
			drawerAround.ItemClick += HandleAroundItemClick;
			drawerAround.Adapter = aroundAdapter = new DrawerAroundAdapter (this);

			drawerMenu.SetItemChecked (0, true);
			if (CheckGooglePlayServices ()) {
				locationClient = new LocationClient (this, this, this);
				SwitchTo (mapFragment = new HubwayMapFragment (this));
				ActionBar.Title = ((IMoyeuSection)mapFragment).Title;
			}
		}


		void HandleAroundItemClick (object sender, AdapterView.ItemClickEventArgs e)
		{
			if (mapFragment != null) {
				drawer.CloseDrawers ();
				mapFragment.CenterAndOpenStationOnMap (e.Id,
				                                       zoom: 17,
				                                       animDurationID: Android.Resource.Integer.ConfigLongAnimTime);
			}
		}

		void HandleSectionItemClick (object sender, AdapterView.ItemClickEventArgs e)
		{
			switch (e.Position - 1) {
			case 1:
				if (mapFragment == null)
					mapFragment = new HubwayMapFragment (this);
				SwitchTo (mapFragment);
				break;
			case 2:
				if (favoriteFragment == null) {
					favoriteFragment = new FavoriteFragment (this, id => {
						SwitchTo (mapFragment);
						mapFragment.CenterAndOpenStationOnMap (id,
						                                       zoom: 17,
						                                       animDurationID: Android.Resource.Integer.ConfigLongAnimTime);
					});
				}
				SwitchTo (favoriteFragment);
				break;
			case 3:
				if (rentalFragment == null)
					rentalFragment = new RentalFragment (this);
				SwitchTo (rentalFragment);
				break;
			default:
				return;
			}
			drawerMenu.SetItemChecked (e.Position, true);
			drawer.CloseDrawers ();
		}

		void SwitchTo (Android.Support.V4.App.Fragment fragment)
		{
			if (fragment.IsVisible)
				return;
			var section = fragment as IMoyeuSection;
			if (section == null)
				return;
			var name = section.Name;
			var t = SupportFragmentManager.BeginTransaction ();
			if (currentFragment == null) {
				t.Add (Resource.Id.content_frame, fragment, name);
				currentFragment = fragment;
			} else {
				t.SetCustomAnimations (Resource.Animator.frag_slide_in,
				                       Resource.Animator.frag_slide_out);
				var existingFragment = SupportFragmentManager.FindFragmentByTag (name);
				if (existingFragment != null)
					existingFragment.View.BringToFront ();
				currentFragment.View.BringToFront ();
				t.Hide (currentFragment);
				if (existingFragment != null) {
					t.Show (existingFragment);
					currentFragment = existingFragment;
				} else {
					t.Add (Resource.Id.content_frame, fragment, name);
					currentFragment = fragment;
				}
				section.RefreshData ();
			}
			t.AddToBackStack (null);
			t.Commit ();
		}

		protected override void OnPostCreate (Bundle savedInstanceState)
		{
			base.OnPostCreate (savedInstanceState);
			drawerToggle.SyncState ();
		}

		public override void OnConfigurationChanged (Android.Content.Res.Configuration newConfig)
		{
			base.OnConfigurationChanged (newConfig);
			drawerToggle.OnConfigurationChanged (newConfig);
		}

		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			if (drawerToggle.OnOptionsItemSelected (item))
				return true;
			return base.OnOptionsItemSelected (item);
		}

		protected override void OnNewIntent (Intent intent)
		{
			base.OnNewIntent (intent);
			if (mapFragment.IsVisible)
				mapFragment.OnSearchIntent (intent);
		}

		protected override void OnStart ()
		{
			if (locationClient != null)
				locationClient.Connect ();
			base.OnStart ();
		}

		protected override void OnStop ()
		{
			if (locationClient != null)
				locationClient.Disconnect ();
			base.OnStop ();
		}

		protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
		{
			if (requestCode == ConnectionFailureResolutionRequest) {
				if (resultCode == Result.Ok && CheckGooglePlayServices ()) {
					if (locationClient == null) {
						locationClient = new LocationClient (this, this, this);
						locationClient.Connect ();
					}
					SwitchTo (mapFragment = new HubwayMapFragment (this));
				} else
					Finish ();
			} else {
				base.OnActivityResult (requestCode, resultCode, data);
			}
		}

		bool CheckGooglePlayServices ()
		{
			var result = GooglePlayServicesUtil.IsGooglePlayServicesAvailable (this);
			if (result == ConnectionResult.Success)
				return true;
			var dialog = GooglePlayServicesUtil.GetErrorDialog (result,
			                                                    this,
			                                                    ConnectionFailureResolutionRequest);
			if (dialog != null) {
				var errorDialog = new ErrorDialogFragment { Dialog = dialog };
				errorDialog.Show (SupportFragmentManager, "Google Services Updates");
				return false;
			}

			Finish ();
			return false;
		}

		#region IObserver implementation

		public void OnCompleted ()
		{
		}

		public void OnError (Exception error)
		{
		}

		public void OnNext (Station[] value)
		{
			if (locationClient == null || !locationClient.IsConnected)
				return;
			var location = locationClient.LastLocation;
			if (location == null)
				return;
			var stations = Hubway.GetStationsAround (value,
				                                     new GeoPoint { Lat = location.Latitude, Lon = location.Longitude },
				                                     minDistance: 1,
				                                     maxItems: 4);
			RunOnUiThread (() => aroundAdapter.SetStations (stations));
		}

		#endregion

		public void OnConnected (Bundle p0)
		{
			if (Hubway.Instance.LastStations != null)
				OnNext (Hubway.Instance.LastStations);
		}

		public void OnDisconnected ()
		{
			
		}

		public void OnConnectionFailed (Android.Gms.Common.ConnectionResult p0)
		{
			
		}
	}

	class DrawerMenuAdapter : BaseAdapter
	{
		Tuple<int, string>[] sections = new Tuple<int, string>[] {
			Tuple.Create (Resource.Drawable.ic_drawer_map, "Map"),
			Tuple.Create (Resource.Drawable.ic_drawer_star, "Favorites"),
			Tuple.Create (Resource.Drawable.ic_drawer_rentals, "Rental History"),
		};

		Context context;

		public DrawerMenuAdapter (Context context)
		{
			this.context = context;
		}

		public override Java.Lang.Object GetItem (int position)
		{
			return new Java.Lang.String (sections [position - 1].Item2);
		}

		public override long GetItemId (int position)
		{
			return position;
		}

		public override View GetView (int position, View convertView, ViewGroup parent)
		{
			if (position == 0 || position > sections.Length) {
				var space = new Space (context);
				space.SetMinimumHeight ((Math.Min (3, position + 1) * 8).ToPixels ());
				return space;
			}
			var view = convertView;
			if (view == null) {
				var inflater = context.GetSystemService (Context.LayoutInflaterService).JavaCast<LayoutInflater> ();
				view = inflater.Inflate (Resource.Layout.DrawerItemLayout, parent, false);
			}
			var icon = view.FindViewById<ImageView> (Resource.Id.icon);
			var text = view.FindViewById<TextView> (Resource.Id.text);

			icon.SetImageResource (sections [position - 1].Item1);
			text.Text = sections [position - 1].Item2;

			return view;
		}

		public override int Count {
			get {
				return sections.Length + 2;
			}
		}

		public override int GetItemViewType (int position)
		{
			return position == 0 || position > sections.Length ? 1 : 0;
		}

		public override int ViewTypeCount {
			get {
				return 2;
			}
		}

		public override bool IsEnabled (int position)
		{
			return true;
		}

		public override bool AreAllItemsEnabled ()
		{
			return false;
		}
	}

	class DrawerAroundAdapter : BaseAdapter
	{
		Context context;

		Station[] stations = null;
		FavoriteManager manager;
		HashSet<int> favorites;

		Drawable starDrawable;

		public DrawerAroundAdapter (Context context)
		{
			this.context = context;
			this.manager = FavoriteManager.Obtain (context);
			this.starDrawable = XamSvg.SvgFactory.GetDrawable (context.Resources, Resource.Raw.star_depressed);
			this.favorites = new HashSet<Int32> ();
			LoadFavorites ();
		}

		public void SetStations (Station[] stations)
		{
			this.stations = stations;
			NotifyDataSetChanged ();
		}

		async void LoadFavorites ()
		{
			if (manager.LastFavorites != null)
				favorites = manager.LastFavorites;
			else
				favorites = await manager.GetFavoriteStationIdsAsync ();
			NotifyDataSetChanged ();
		}

		public void Refresh ()
		{
			LoadFavorites ();
		}

		public override Java.Lang.Object GetItem (int position)
		{
			return new Java.Lang.String (stations [position].Name);
		}

		public override long GetItemId (int position)
		{
			return stations [position].Id;
		}

		public override View GetView (int position, View convertView, ViewGroup parent)
		{
			var view = convertView;
			if (view == null) {
				var inflater = context.GetSystemService (Context.LayoutInflaterService).JavaCast<LayoutInflater> ();
				view = inflater.Inflate (Resource.Layout.DrawerAroundItem, parent, false);
			}

			var star = view.FindViewById<ImageView> (Resource.Id.aroundStar);
			var stationName = view.FindViewById<TextView> (Resource.Id.aroundStation1);
			var stationNameSecond = view.FindViewById<TextView> (Resource.Id.aroundStation2);
			var bikes = view.FindViewById<TextView> (Resource.Id.aroundBikes);
			var racks = view.FindViewById<TextView> (Resource.Id.aroundRacks);

			var station = stations [position];
			star.SetImageDrawable (starDrawable);
			star.Visibility = favorites.Contains (station.Id) ? ViewStates.Visible : ViewStates.Invisible;
			string secondPart;
			stationName.Text = Hubway.CutStationName (station.Name, out secondPart);
			stationNameSecond.Text = secondPart;
			bikes.Text = station.BikeCount.ToString ();
			racks.Text = station.EmptySlotCount.ToString ();

			return view;
		}

		public override int Count {
			get {
				return stations == null ? 0 : stations.Length;
			}
		}

		public override bool IsEnabled (int position)
		{
			return true;
		}

		public override bool AreAllItemsEnabled ()
		{
			return false;
		}
	}

	class ErrorDialogFragment : Android.Support.V4.App.DialogFragment
	{
		public new Dialog Dialog {
			get;
			set;
		}

		public override Dialog OnCreateDialog (Bundle savedInstanceState)
		{
			return Dialog;
		}
	}

	class MoyeuActionBarToggle : ActionBarDrawerToggle
	{
		public MoyeuActionBarToggle (Activity activity,
		                             DrawerLayout drawer,
		                             int dImageRes,
		                             int openDrawerContentRes,
		                             int closeDrawerContentRest)
			: base (activity, drawer, dImageRes, openDrawerContentRes, closeDrawerContentRest)
		{
		}

		public Action OpenCallback {
			get;
			set;
		}

		public Action CloseCallback {
			get;
			set;
		}

		public override void OnDrawerOpened (View drawerView)
		{
			base.OnDrawerOpened (drawerView);
			if (OpenCallback != null)
				OpenCallback ();
		}

		public override void OnDrawerClosed (View drawerView)
		{
			base.OnDrawerClosed (drawerView);
			if (CloseCallback != null)
				CloseCallback ();
		}
	}
}


