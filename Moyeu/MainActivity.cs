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
using ConnectionCallbacks = Android.Gms.Common.Apis.GoogleApiClient.IConnectionCallbacks;
using ConnectionFailedListener = Android.Gms.Common.Apis.GoogleApiClient.IOnConnectionFailedListener;

using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Support.V4.Graphics.Drawable;
using Android.Support.V4.View;
using Android.Support.V4.Content;
using Android.Support.Design.Widget;
using Android.Util;

namespace Moyeu
{
	[Activity (Label = "Moyeu",
	           MainLauncher = true,
	           Theme = "@style/MoyeuTheme",
	           ScreenOrientation = ScreenOrientation.Portrait,
	           ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
	           LaunchMode = LaunchMode.SingleTop)]
	[IntentFilter (new[] { "android.intent.action.SEARCH", "com.google.android.gms.actions.SEARCH_ACTION" }, Categories = new[] { "android.intent.category.DEFAULT" })]
	[MetaData ("android.app.searchable", Resource = "@xml/searchable")]
	public class MainActivity
		: Android.Support.V7.App.AppCompatActivity, IObserver<Station[]>, ConnectionCallbacks, ConnectionFailedListener
	{
		const int ConnectionFailureResolutionRequest = 9000;
		const int GpsNotAvailableResolutionRequest = 90001;
		const int LocationPermissionRequest = 101;
		const string DialogError = "dialog_error";

		HubwayMapFragment mapFragment;
		FavoriteFragment favoriteFragment;
		RentalMaterialFragment rentalFragment;

		DrawerLayout drawer;
		Android.Support.V7.App.ActionBarDrawerToggle drawerToggle;
		ListView drawerAround;
		NavigationView drawerMenu;

		DrawerAroundAdapter aroundAdapter;
		GoogleApiClient client;
		bool apiClientResolvingError;

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

			var toolbar = FindViewById<Android.Support.V7.Widget.Toolbar> (Resource.Id.toolbar);
			ViewCompat.SetElevation (toolbar, TypedValue.ApplyDimension (ComplexUnitType.Dip, 2, Resources.DisplayMetrics));
			SetSupportActionBar (toolbar);

			this.drawer = FindViewById<DrawerLayout> (Resource.Id.drawer_layout);
			drawer.SetDrawerShadow (Resource.Drawable.drawer_shadow, (int)GravityFlags.Start);

			drawerToggle = new Android.Support.V7.App.ActionBarDrawerToggle (this,
			                                                                 drawer,
			                                                                 Resource.String.open_drawer,
			                                                                 Resource.String.close_drawer);
			drawer.SetDrawerListener (drawerToggle);

			SupportActionBar.SetDisplayHomeAsUpEnabled (true);
			SupportActionBar.SetHomeButtonEnabled (true);

			Hubway.Instance.Subscribe (this);
			FavoriteManager.FavoritesChanged += (sender, e) => aroundAdapter.Refresh ();

			drawerAround = FindViewById<ListView> (Resource.Id.left_drawer_around);
			drawerAround.ItemClick += HandleAroundItemClick;
			drawerAround.Adapter = aroundAdapter = new DrawerAroundAdapter (this);

			drawerMenu = FindViewById<NavigationView> (Resource.Id.left_drawer);
			drawerMenu.NavigationItemSelected += HandlerNavigationItemSelected;

			if (CheckGooglePlayServices ())
				PostCheckGooglePlayServices ();
		}

		GoogleApiClient CreateApiClient ()
		{
			return new GoogleApiClient.Builder (this, this, this)
				.AddApi (LocationServices.API)
				.Build ();
		}

		void HandlerNavigationItemSelected (object sender, NavigationView.NavigationItemSelectedEventArgs e)
		{
			var item = e.MenuItem;
			SwitchToSectionPosition (item.Order);
			e.Handled = true;
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
					rentalFragment = new RentalMaterialFragment ();
				SwitchTo (rentalFragment);
				break;
			default:
				return;
			}
			var item = drawerMenu.Menu.GetItem (position);
			if (item != null)
				item.SetChecked (true);
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
				var existingFragment = SupportFragmentManager.FindFragmentByTag (name);
				if (!AndroidExtensions.IsMaterial) {
					t.SetCustomAnimations (Resource.Animator.frag_slide_in,
					                       Resource.Animator.frag_slide_out);
					if (existingFragment != null)
						existingFragment.View.BringToFront ();
					currentFragment.View.BringToFront ();
				} else {
					currentFragment.View.BringToFront ();
					if (existingFragment != null)
						existingFragment.View.BringToFront ();
				}
				t.Hide (CurrentFragment);
				if (existingFragment != null)
					t.Show (existingFragment);
				else
					t.Add (Resource.Id.content_frame, fragment, name);
				section.RefreshData ();
			}
			t.Commit ();
			SupportActionBar.Title = section.Title;
		}

		internal void SwitchToMapAndShowStationWithId (long id)
		{
			SwitchToSectionPosition (0);
			SupportActionBar.Title = mapFragment.Title;
			var animDuration = Android.Resource.Integer.ConfigLongAnimTime;
			mapFragment.CenterAndOpenStationOnMap (id, zoom: 17, animDurationID: animDuration);
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
			if (DrawerToggleOnOptionsItemSelected (item))
				return true;
			return base.OnOptionsItemSelected (item);
		}

		bool DrawerToggleOnOptionsItemSelected (IMenuItem item)
		{
			return drawerToggle.OnOptionsItemSelected (item);
		}

		protected override void OnNewIntent (Intent intent)
		{
			base.OnNewIntent (intent);
			CheckForSearchTermInIntent (intent);
		}

		void CheckForSearchTermInIntent (Intent intent)
		{
			if (intent.Action == Intent.ActionSearch
			    || intent.Action == "com.google.android.gms.actions.SEARCH_ACTION")
				mapFragment.OnSearchIntent (intent);
		}

		protected override void OnStart ()
		{
			if (client != null && !apiClientResolvingError)
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
				apiClientResolvingError = false;
				if (resultCode == Result.Ok) {
					if (!client.IsConnecting && !client.IsConnected)
						client.Connect ();
				}
			} else if (requestCode == GpsNotAvailableResolutionRequest) {
				if (resultCode == Result.Ok && CheckGooglePlayServices ())
					PostCheckGooglePlayServices ();
				else
					Finish ();
			} else {
				base.OnActivityResult (requestCode, resultCode, data);
			}
		}

		public override void OnRequestPermissionsResult (int requestCode, string[] permissions, Permission[] grantResults)
		{
			if (requestCode == LocationPermissionRequest) {
				if (grantResults.Length == 0 || grantResults [0] == Permission.Denied) {
					FindViewById (Resource.Id.aroundLayout).Visibility = ViewStates.Invisible;
					return;
				} else if (Hubway.Instance.LastStations != null) {
					OnNext (Hubway.Instance.LastStations);
				}
			} else
				base.OnRequestPermissionsResult (requestCode, permissions, grantResults);
		}

		public override void OnBackPressed ()
		{
			var currentSection = CurrentFragment as IMoyeuSection;
			if (currentSection != null && currentSection.OnBackPressed ())
				return;
			base.OnBackPressed ();
		}

		bool CheckGooglePlayServices ()
		{
			var result = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable (this);
			if (result == ConnectionResult.Success)
				return true;
			var shown = GoogleApiAvailability.Instance.ShowErrorDialogFragment (this,
			                                                                    result,
			                                                                    GpsNotAvailableResolutionRequest);
			if (!shown)
				Finish ();
			
			return false;
		}

		void PostCheckGooglePlayServices ()
		{
			if (client == null)
				client = CreateApiClient ();
			SwitchTo (mapFragment = new HubwayMapFragment ());
			SupportActionBar.Title = ((IMoyeuSection)mapFragment).Title;
			CheckForSearchTermInIntent (Intent);
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
			var locPerm = ActivityCompat.CheckSelfPermission (this, Android.Manifest.Permission.AccessFineLocation);
			if (locPerm != (int)Permission.Granted) {
				ActivityCompat.RequestPermissions (this,
				                                   new[] { Android.Manifest.Permission.AccessFineLocation },
				                                   LocationPermissionRequest);
				return;
			}
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

		public void OnConnected (Bundle connectionHint)
		{
			if (Hubway.Instance.LastStations != null)
				OnNext (Hubway.Instance.LastStations);
		}

		public void OnConnectionSuspended (int cause)
		{

		}

		public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
			if (apiClientResolvingError)
				return;
			
			if (result.HasResolution) {
				try {
					apiClientResolvingError = true;
					result.StartResolutionForResult (this, ConnectionFailureResolutionRequest);
				} catch (Android.Content.IntentSender.SendIntentException) {
					client.Connect ();
				}
			} else {
				var args = new Bundle ();
				args.PutInt (DialogError, result.ErrorCode);
				var dialogFragment = new ErrorDialogFragment () {
					Arguments = args
				};
				dialogFragment.Show (SupportFragmentManager, "errordialog");
			}
		}

		void OnGpsDialogDismissed ()
		{
			apiClientResolvingError = false;
		}

		class ErrorDialogFragment : Android.Support.V4.App.DialogFragment
		{
			public ErrorDialogFragment () {}

			public override Dialog OnCreateDialog (Bundle savedInstanceState)
			{
				int errorCode = Arguments.GetInt (DialogError);
				return GoogleApiAvailability.Instance.GetErrorDialog (Activity,
				                                                      errorCode,
				                                                      ConnectionFailureResolutionRequest);
			}

			public override void OnDismiss (IDialogInterface dialog)
			{
				((MainActivity)Activity).OnGpsDialogDismissed ();
			}
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
			this.starDrawable = DrawableCompat.Wrap (ContextCompat.GetDrawable (context, Resource.Drawable.ic_small_star));
			DrawableCompat.SetTint (starDrawable, ContextCompat.GetColor (context, Resource.Color.white_tint_secondary));
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

	class MoyeuActionBarToggle : Android.Support.V7.App.ActionBarDrawerToggle
	{
		public MoyeuActionBarToggle (Activity activity,
		                             DrawerLayout drawer,
		                             int openDrawerContentRes,
		                             int closeDrawerContentRest)
			: base (activity, drawer, openDrawerContentRes, closeDrawerContentRest)
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


