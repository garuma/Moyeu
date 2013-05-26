using System;
using System.Linq;
using System.Collections.Generic;

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

namespace Moyeu
{
	[Activity (Label = "Moyeu",
	           MainLauncher = true,
	           Theme = "@android:style/Theme.Holo.Light.DarkActionBar",
	           UiOptions = UiOptions.SplitActionBarWhenNarrow,
	           ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize,
	           LaunchMode = LaunchMode.SingleTop)]
	[IntentFilter (new[] { "android.intent.action.SEARCH" }, Categories = new[] { "android.intent.category.DEFAULT" })]
	[MetaData ("android.app.searchable", Resource = "@xml/searchable")]
	public class MainActivity : Activity
	{
		Dictionary<int, Marker> existingMarkers = new Dictionary<int, Marker> ();
		Marker locationPin;
		MapFragment mapFragment;
		Hubway hubway = Hubway.Instance;
		InfoWindowAdapter infoAdapter;
		bool loading;
		bool? showedLongClickHint;
		FlashBarController flashBar;
		IMenuItem searchItem;
		FavoriteManager favManager;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			DensityExtensions.Initialize (this);
			Window.RequestFeature (WindowFeatures.ActionBarOverlay);
			SetContentView (Resource.Layout.Main);
			var barBackgroundDrawable = new ColorDrawable (new Color (0, 0, 0, 200));
			ActionBar.SetBackgroundDrawable (barBackgroundDrawable);
			ActionBar.SetSplitBackgroundDrawable (barBackgroundDrawable);

			this.favManager = new FavoriteManager (this);
			this.mapFragment = (MapFragment)FragmentManager.FindFragmentById (Resource.Id.map);
			this.infoAdapter = new InfoWindowAdapter (this);
			mapFragment.Map.SetInfoWindowAdapter (infoAdapter);
			mapFragment.Map.MyLocationEnabled = true;
			mapFragment.Map.UiSettings.MyLocationButtonEnabled = false;
			mapFragment.Map.UiSettings.CompassEnabled = false;
			mapFragment.Map.UiSettings.RotateGesturesEnabled = false;
			mapFragment.Map.InfoWindowClick += HandleInfoWindowClick;
			mapFragment.Map.MapLongClick += HandleMapLongClick;
			mapFragment.Map.MarkerClick += HandleMarkerClick;

			flashBar = new FlashBarController (this);
		}

		void HandleMarkerClick (object sender, GoogleMap.MarkerClickEventArgs e)
		{	
			if (!ShowedLongClickHint) {
				ShowedLongClickHint = true;
				flashBar.ShowInformation ("Long-click to view station in Google Maps");
			}
			e.Handled = false;
		}

		void HandleMapLongClick (object sender, GoogleMap.MapLongClickEventArgs e)
		{
			if (hubway.LastStations == null)
				return;

			var position = new GeoPoint { Lat = e.P0.Latitude, Lon = e.P0.Longitude };
			var closestStation = hubway
				.GetClosestStationTo (hubway.LastStations, position)
				.FirstOrDefault ();
			if (GeoUtils.Distance (closestStation.Location, position) > .200)
				return;

			var location = closestStation.GeoUrl;
			var uri = Android.Net.Uri.Parse (location);
			var intent = new Intent (Intent.ActionView, uri);
			StartActivity (intent);
		}

		void HandleInfoWindowClick (object sender, GoogleMap.InfoWindowClickEventArgs e)
		{
			var markerID = int.Parse (e.P0.Title.Split ('|')[0]);
			bool contained = favManager.GetFavoritesStationIds ().Contains (markerID);
			if (contained)
				favManager.RemoveFromFavorite (markerID);
			else
				favManager.AddToFavorite (markerID);
			infoAdapter.ToggleStar ();
			e.P0.ShowInfoWindow ();
		}

		protected override void OnStart ()
		{
			base.OnStart ();
			FillUpMap (false);
		}

		async void FillUpMap (bool forceRefresh)
		{
			if (loading)
				return;
			loading = true;
			flashBar.ShowLoading ();

			try {
				var stations = await hubway.GetStations (forceRefresh);
				foreach (var station in stations) {
					Marker marker;
					var stats = station.BikeCount + "|" + station.EmptySlotCount;
					if (existingMarkers.TryGetValue (station.Id, out marker)) {
						if (marker.Snippet == stats)
							continue;
						marker.Remove ();
					}

					var hue = BitmapDescriptorFactory.HueGreen * (float)TruncateDigit (station.BikeCount / ((float)station.Capacity), 2);
					var markerOptions = new MarkerOptions ()
						.SetTitle (station.Id + "|" + station.Name)
						.SetSnippet (station.BikeCount + "|" + station.EmptySlotCount)
						.SetPosition (new Android.Gms.Maps.Model.LatLng (station.Location.Lat, station.Location.Lon))
						.InvokeIcon (BitmapDescriptorFactory.DefaultMarker (hue));
					existingMarkers[station.Id] = mapFragment.Map.AddMarker (markerOptions);
				}
			} catch (Exception e) {
				Android.Util.Log.Debug ("DataFetcher", e.ToString ());
			}

			flashBar.ShowLoaded ();
			loading = false;
		}

		public override bool OnCreateOptionsMenu (IMenu menu)
		{
			var inflater = MenuInflater;
			inflater.Inflate (Resource.Menu.map_menu, menu);
			searchItem = menu.FindItem (Resource.Id.menu_search);
			SetupSearchInput ((SearchView)searchItem.ActionView);
			return true;
		}

		void SetupSearchInput (SearchView searchView)
		{
			var searchManager = GetSystemService (Context.SearchService).JavaCast<SearchManager> ();
			searchView.SetIconifiedByDefault (false);
			var searchInfo = searchManager.GetSearchableInfo (ComponentName);
			searchView.SetSearchableInfo (searchInfo);
		}

		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			switch (item.ItemId) {
			case Resource.Id.menu_locate:
				var map = mapFragment.Map;
				var location = map.MyLocation;
				if (location == null)
					return false;
				var userPos = new LatLng (location.Latitude, location.Longitude);
				var camPos = map.CameraPosition.Target;
				var needZoom = TruncateDigit (camPos.Latitude, 4) == TruncateDigit (userPos.Latitude, 4)
					&& TruncateDigit (camPos.Longitude, 4) == TruncateDigit (userPos.Longitude, 4);
				var cameraUpdate = needZoom ?
					CameraUpdateFactory.NewLatLngZoom (userPos, map.CameraPosition.Zoom + 2) :
					CameraUpdateFactory.NewLatLng (userPos);
				map.AnimateCamera (cameraUpdate);
				break;
			case Resource.Id.menu_refresh:
				FillUpMap (forceRefresh: true);
				break;
			case Resource.Id.menu_star:
				StartActivity (typeof (FavoriteActivity));
				break;
			default:
				return base.OnOptionsItemSelected (item);
			}

			return true;
		}

		protected override void OnNewIntent (Intent intent)
		{
			base.OnNewIntent (intent);
			searchItem.CollapseActionView ();
			if (intent.Action != Intent.ActionSearch)
				return;
			var serial = (string)intent.Extras.Get (SearchManager.ExtraDataKey);
			if (serial == null)
				return;
			var latlng = serial.Split ('|');
			var finalLatLng = new LatLng (double.Parse (latlng[0]),
			                              double.Parse (latlng[1]));

			var camera = CameraUpdateFactory.NewLatLngZoom (finalLatLng, 16);
			mapFragment.Map.AnimateCamera (camera,
			                               new MapAnimCallback (() => SetLocationPin (finalLatLng)));
		}

		void SetLocationPin (LatLng finalLatLng)
		{
			if (locationPin != null) {
				locationPin.Remove ();
				locationPin = null;
			}
			var proj = mapFragment.Map.Projection;
			var location = proj.ToScreenLocation (finalLatLng);
			location.Offset (0, -(35.ToPixels ()));
			var startLatLng = proj.FromScreenLocation (location);

			new Handler (MainLooper).PostDelayed (() => {
				var opts = new MarkerOptions ()
					.SetPosition (startLatLng)
						.InvokeIcon (BitmapDescriptorFactory.DefaultMarker (BitmapDescriptorFactory.HueViolet));
				var marker = mapFragment.Map.AddMarker (opts);
				var animator = ObjectAnimator.OfObject (marker, "position", new LatLngEvaluator (), startLatLng, finalLatLng);
				animator.SetDuration (1000);
				animator.SetInterpolator (new Android.Views.Animations.BounceInterpolator ());
				animator.Start ();
				locationPin = marker;
			}, 800);
		}

		class LatLngEvaluator : Java.Lang.Object, ITypeEvaluator
		{
			public Java.Lang.Object Evaluate (float fraction, Java.Lang.Object startValue, Java.Lang.Object endValue)
			{
				var start = (LatLng)startValue;
				var end = (LatLng)endValue;

				return new LatLng (start.Latitude + fraction * (end.Latitude - start.Latitude),
				                   start.Longitude + fraction * (end.Longitude - start.Longitude));
			}
		}

		class MapAnimCallback : Java.Lang.Object, GoogleMap.ICancelableCallback
		{
			Action callback;

			public MapAnimCallback (Action callback)
			{
				this.callback = callback;
			}

			public void OnCancel ()
			{
			}

			public void OnFinish ()
			{
				if (callback != null)
					callback ();
			}
		}

		double TruncateDigit (double d, int digitNumber)
		{
			var power = Math.Pow (10, digitNumber);
			return Math.Truncate (d * power) / power;
		}

		bool ShowedLongClickHint {
			get {
				if (showedLongClickHint == null)
					showedLongClickHint = GetPreferences (FileCreationMode.Private)
						.GetBoolean ("showedLongClickHint", false);
				return (bool)showedLongClickHint;
			}
			set {
				var prefs = GetPreferences (FileCreationMode.Private);
				prefs.Edit ().PutBoolean ("showedLongClickHint", value).Commit ();
				showedLongClickHint = value;
			}
		}
	}
}


