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
using Android.Gms.Common.Apis;
using ConnectionCallbacks = Android.Gms.Common.Apis.IGoogleApiClientConnectionCallbacks;
using ConnectionFailedListener = Android.Gms.Common.Apis.IGoogleApiClientOnConnectionFailedListener;

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

		DrawerLayout drawer;
		ActionBarDrawerToggle drawerToggle;
		ListView drawerMenu;
		ListView drawerAround;

		DrawerAroundAdapter aroundAdapter;
		IGoogleApiClient client;

		Typeface menuNormalTf, menuHighlightTf;

		Android.Support.V4.App.Fragment CurrentFragment {
			get {
				return new Android.Support.V4.App.Fragment[] {
					mapFragment,
					favoriteFragment,
					rentalFragment
				}.FirstOrDefault (f => f != null && f.IsAdded && f.IsVisible);
			}
		}

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			AndroidExtensions.Initialize (this);
			AnalyticsHelper.Initialize (ApplicationContext);
			SetContentView (Resource.Layout.Main);

			this.drawer = FindViewById<DrawerLayout> (Resource.Id.drawer_layout);
			this.drawerToggle = new MoyeuActionBarToggle (this,
			                                              drawer,
			                                              Resource.Drawable.ic_drawer,
			                                              Resource.String.open_drawer,
			                                              Resource.String.close_drawer) {
				OpenCallback = () => {
					ActionBar.Title = Title;
					CurrentFragment.HasOptionsMenu = false;
					InvalidateOptionsMenu ();
				},
				CloseCallback = () => {
					var currentFragment = CurrentFragment;
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
			drawerMenu.AddFooterView (new Space (this));
			drawerMenu.ItemClick += HandleSectionItemClick;
			menuNormalTf = Typeface.Create (Resources.GetString (Resource.String.menu_item_fontFamily),
			                                TypefaceStyle.Normal);
			menuHighlightTf = Typeface.Create (Resources.GetString (Resource.String.menu_item_fontFamily),
			                                   TypefaceStyle.Bold);
			drawerMenu.Adapter = new DrawerMenuAdapter (this);

			drawerAround = FindViewById<ListView> (Resource.Id.left_drawer_around);
			drawerAround.ItemClick += HandleAroundItemClick;
			drawerAround.Adapter = aroundAdapter = new DrawerAroundAdapter (this);

			drawerMenu.SetItemChecked (0, true);
			if (CheckGooglePlayServices ())
				PostCheckGooglePlayServices ();
		}

		IGoogleApiClient CreateApiClient ()
		{
			return new GoogleApiClientBuilder (this, this, this)
				.AddApi (LocationServices.Api)
				.Build ();
		}

		void HandleAroundItemClick (object sender, AdapterView.ItemClickEventArgs e)
		{
			SwitchToMapAndShowStationWithId (e.Id);
		}

		void HandleSectionItemClick (object sender, AdapterView.ItemClickEventArgs e)
		{
			SwitchToSectionPosition (e.Position);
		}

		void SwitchToSectionPosition (int position)
		{
			switch (position) {
			case 0:
				if (mapFragment == null)
					mapFragment = new HubwayMapFragment ();
				SwitchTo (mapFragment);
				break;
			case 1:
				if (favoriteFragment == null)
					favoriteFragment = new FavoriteFragment ();
				SwitchTo (favoriteFragment);
				break;
			case 2:
				if (rentalFragment == null)
					rentalFragment = new RentalFragment ();
				SwitchTo (rentalFragment);
				break;
			default:
				return;
			}
			SetSelectedMenuIndex (position);
			drawerMenu.SetItemChecked (position, true);
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
			var currentFragment = CurrentFragment;
			if (currentFragment == null) {
				t.Add (Resource.Id.content_frame, fragment, name);
			} else {
				t.SetCustomAnimations (Resource.Animator.frag_slide_in,
				                       Resource.Animator.frag_slide_out);
				var existingFragment = SupportFragmentManager.FindFragmentByTag (name);
				if (existingFragment != null)
					existingFragment.View.BringToFront ();
				currentFragment.View.BringToFront ();
				t.Hide (CurrentFragment);
				if (existingFragment != null) {
					t.Show (existingFragment);
				} else {
					t.Add (Resource.Id.content_frame, fragment, name);
				}
				section.RefreshData ();
			}
			t.Commit ();
		}

		internal void SwitchToMapAndShowStationWithId (long id)
		{
			SwitchToSectionPosition (0);
			ActionBar.Title = mapFragment.Title;
			var animDuration = Android.Resource.Integer.ConfigLongAnimTime;
			mapFragment.CenterAndOpenStationOnMap (id, zoom: 17, animDurationID: animDuration);
		}

		void SetSelectedMenuIndex (int pos)
		{
			for (int i = 0; i < 3; i++) {
				var text = (TextView)drawerMenu.GetChildAt (i);
				text.Typeface = i == pos ? menuHighlightTf : menuNormalTf;
			}
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
			if (client != null)
				client.Connect ();
			base.OnStart ();
		}

		protected override void OnStop ()
		{
			if (client != null)
				client.Disconnect ();
			base.OnStop ();
		}

		protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
		{
			if (requestCode == ConnectionFailureResolutionRequest) {
				if (resultCode == Result.Ok && CheckGooglePlayServices ()) {
					PostCheckGooglePlayServices ();
					client.Connect ();
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

		void PostCheckGooglePlayServices ()
		{
			MapsInitializer.Initialize (this);
			if (client == null)
				client = CreateApiClient ();
			SwitchTo (mapFragment = new HubwayMapFragment ());
			ActionBar.Title = ((IMoyeuSection)mapFragment).Title;
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
			if (client == null || !client.IsConnected)
				return;
			var location = LocationServices.FusedLocationApi.GetLastLocation (client);
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

		public void OnConnectionSuspended (int reason)
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
			var text = convertView as TextView;
			if (text == null) {
				var inflater = context.GetSystemService (Context.LayoutInflaterService).JavaCast<LayoutInflater> ();
				text = (TextView)inflater.Inflate (Resource.Layout.DrawerItemLayout, parent, false);
			}
			// Initial menu initialization, put the first item as selected
			if (position == 0 && convertView == null)
				text.SetTypeface (text.Typeface, TypefaceStyle.Bold);
			text.Text = sections [position].Item2;
			text.SetCompoundDrawablesWithIntrinsicBounds (sections [position].Item1, 0, 0, 0);

			return text;
		}

		public override int Count {
			get {
				return sections.Length;
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
			stationName.Text = StationUtils.CutStationName (station.Name, out secondPart);
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


