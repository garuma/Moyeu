using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

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
using Android.Util;

using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Gms.Location;

using XamSvg;
using Android.Support.V4.View;
using Android.Support.V4.Graphics.Drawable;

namespace Moyeu
{
	public class HubwayMapFragment: Android.Support.V4.App.Fragment, ViewTreeObserver.IOnGlobalLayoutListener, IMoyeuSection, IOnMapReadyCallback, IOnStreetViewPanoramaReadyCallback
	{
		Dictionary<int, Marker> existingMarkers = new Dictionary<int, Marker> ();
		Marker locationPin;
		MapView mapFragment;
		GoogleMap map;
		StreetViewPanoramaView streetViewFragment;
		StreetViewPanorama streetPanorama;
		Hubway hubway = Hubway.Instance;
		HubwayHistory hubwayHistory = new HubwayHistory ();

		bool loading;
		bool showedStale;
		string pendingSearchTerm;
		FlashBarController flashBar;
		IMenuItem searchItem;
		FavoriteManager favManager;
		TextView lastUpdateText;
		PinFactory pinFactory;
		InfoPane pane;

		const string SearchPinId = "SEARCH_PIN";
		int currentShownID = -1;
		Marker currentShownMarker;
		CameraPosition oldPosition;

		public HubwayMapFragment ()
		{
			HasOptionsMenu = true;
			AnimationExtensions.SetupFragmentTransitions (this);
		}

		public string Name {
			get {
				return "MapFragment";
			}
		}

		public string Title {
			get {
				return "Map";
			}
		}

		internal int CurrentShownId {
			get {
				return currentShownID;
			}
		}

		public void RefreshData ()
		{
			FillUpMap (forceRefresh: false);
		}

		public override void OnActivityCreated (Bundle savedInstanceState)
		{
			base.OnActivityCreated (savedInstanceState);

			var context = Activity;
			this.pinFactory = new PinFactory (context);
			this.favManager = FavoriteManager.Obtain (context);
		}

		public override void OnStart ()
		{
			base.OnStart ();
			RefreshData ();
		}

		public void OnGlobalLayout ()
		{
			Activity.RunOnUiThread (() => pane.SetState (InfoPane.State.Closed, animated: false));
			View.ViewTreeObserver.RemoveGlobalOnLayoutListener (this);
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			var view = inflater.Inflate (Resource.Layout.MapLayout, container, false);
			mapFragment = view.FindViewById<MapView> (Resource.Id.map);
			mapFragment.OnCreate (savedInstanceState);
			lastUpdateText = view.FindViewById<TextView> (Resource.Id.UpdateTimeText);
			SetupInfoPane (view);
			flashBar = new FlashBarController (view);
			streetViewFragment = view.FindViewById<StreetViewPanoramaView> (Resource.Id.streetViewPanorama);
			streetViewFragment.OnCreate (savedInstanceState);

			// For some reason, a recent version of GPS set their surfaceview to be drawn un-composited.
			FixupStreetViewSurface (streetViewFragment);

			return view;
		}

		void FixupStreetViewSurface (View baseView)
		{
			var surfaceView = FindSurfaceView (baseView);
			if (surfaceView != null)
				surfaceView.SetZOrderMediaOverlay (true);
		}

		SurfaceView FindSurfaceView (View baseView)
		{
			var surfaceView = baseView as SurfaceView;
			if (surfaceView != null)
				return surfaceView;
			var viewGrp = baseView as ViewGroup;
			if (viewGrp == null)
				return null;
			for (int i = 0; i < viewGrp.ChildCount; i++) {
				surfaceView = FindSurfaceView (viewGrp.GetChildAt (i));
				if (surfaceView != null)
					return surfaceView;
			}

			return null;
		}

		void SetupInfoPane (View view)
		{
			pane = view.FindViewById<InfoPane> (Resource.Id.infoPane);
			pane.StateChanged += HandlePaneStateChanged;
			view.ViewTreeObserver.AddOnGlobalLayoutListener (this);
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);

			view.Background = AndroidExtensions.DefaultBackground;

			mapFragment.GetMapAsync (this);

			// Setup info pane
			var bikes = pane.FindViewById<TextView> (Resource.Id.InfoViewBikeNumber);
			var slots = pane.FindViewById<TextView> (Resource.Id.InfoViewSlotNumber);
			if (!AndroidExtensions.IsMaterial) {
				var bikeDrawable = DrawableCompat.Wrap (Resources.GetDrawable (Resource.Drawable.ic_bike));
				var lockDrawable = DrawableCompat.Wrap (Resources.GetDrawable (Resource.Drawable.ic_lock));
				bikes.SetCompoundDrawablesWithIntrinsicBounds (null, null, null, bikeDrawable);
				slots.SetCompoundDrawablesWithIntrinsicBounds (null, null, null, lockDrawable);
			} else {
				bikes.SetCompoundDrawablesWithIntrinsicBounds (0, 0, 0, Resource.Drawable.ic_bike_vector);
				slots.SetCompoundDrawablesWithIntrinsicBounds (0, 0, 0, Resource.Drawable.ic_racks_vector);
			}

			var starBtn = pane.FindViewById (Resource.Id.StarButton);
			starBtn.Click += HandleStarButtonChecked;

			streetViewFragment.GetStreetViewPanoramaAsync (this);
		}

		void SetSvgImage (View baseView, int viewId, int resId)
		{
			var view = baseView.FindViewById<ImageView> (viewId);
			if (view == null)
				return;
			var img = SvgFactory.GetDrawable (Resources, resId);
			view.SetImageDrawable (img);
		}

		public void OnMapReady (GoogleMap googleMap)
		{
			this.map = googleMap;
			MapsInitializer.Initialize (Activity.ApplicationContext);

			// Default map initialization
			googleMap.MyLocationEnabled = true;
			googleMap.UiSettings.MyLocationButtonEnabled = false;

			googleMap.MarkerClick += HandleMarkerClick;
			googleMap.MapClick += HandleMapClick;
			var oldPosition = PreviousCameraPosition;
			if (oldPosition != null)
				googleMap.MoveCamera (CameraUpdateFactory.NewCameraPosition (oldPosition));
		}

		public void OnStreetViewPanoramaReady (StreetViewPanorama panorama)
		{
			this.streetPanorama = panorama;
			panorama.UserNavigationEnabled = false;
			panorama.StreetNamesEnabled = false;
			panorama.StreetViewPanoramaClick += HandleMapButtonClick;
		}

		void HandlePaneStateChanged (InfoPane.State state)
		{
			if (map == null)
				return;
			var time = Resources.GetInteger (Android.Resource.Integer.ConfigShortAnimTime);
			var enabled = state != InfoPane.State.FullyOpened;
			map.UiSettings.ScrollGesturesEnabled = enabled;
			map.UiSettings.ZoomGesturesEnabled = enabled;
			if (state == InfoPane.State.FullyOpened && currentShownMarker != null) {
				oldPosition = map.CameraPosition;
				var destX = mapFragment.Width / 2;
				var destY = (mapFragment.Height - pane.Height) / 2;
				var currentPoint = map.Projection.ToScreenLocation (currentShownMarker.Position);
				var scroll = CameraUpdateFactory.ScrollBy (- destX + currentPoint.X, - destY + currentPoint.Y);
				map.AnimateCamera (scroll, time, null);
			} else if (oldPosition != null) {
				map.AnimateCamera (CameraUpdateFactory.NewCameraPosition (oldPosition), time, null);
				oldPosition = null;
			}
		}

		void HandleMapButtonClick (object sender, StreetViewPanorama.StreetViewPanoramaClickEventArgs e)
		{
			var stations = hubway.LastStations;
			if (stations == null || currentShownID == -1)
				return;

			var stationIndex = Array.FindIndex (stations, s => s.Id == currentShownID);
			if (stationIndex == -1)
				return;
			var station = stations [stationIndex];
			var location = station.GeoUrl;
			var uri = Android.Net.Uri.Parse (location);
			var intent = new Intent (Intent.ActionView, uri);
			StartActivity (intent);
		}

		public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
		{
			inflater.Inflate (Resource.Menu.map_menu, menu);
			searchItem = menu.FindItem (Resource.Id.menu_search);
			var searchView = MenuItemCompat.GetActionView (searchItem);
			SetupSearchInput (searchView.JavaCast<Android.Support.V7.Widget.SearchView> ());
		}

		void SetupSearchInput (Android.Support.V7.Widget.SearchView searchView)
		{
			var searchManager = Activity.GetSystemService (Context.SearchService).JavaCast<SearchManager> ();
			searchView.SetIconifiedByDefault (false);
			var searchInfo = searchManager.GetSearchableInfo (Activity.ComponentName);
			searchView.SetSearchableInfo (searchInfo);
		}

		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			if (item.ItemId == Resource.Id.menu_refresh) {
				FillUpMap (forceRefresh: true);
				return true;
			} else if (item.ItemId == Resource.Id.menu_mylocation) {
				CenterMapOnUser ();
				return true;
			}
			return base.OnOptionsItemSelected (item);
		}

		public override void OnViewStateRestored (Bundle savedInstanceState)
		{
			base.OnViewStateRestored (savedInstanceState);
			if (savedInstanceState != null && savedInstanceState.ContainsKey ("previousPosition")) {
				var pos = savedInstanceState.GetParcelable ("previousPosition") as CameraPosition;
				if (pos != null && map != null) {
					var update = CameraUpdateFactory.NewCameraPosition (pos);
					map.MoveCamera (update);
				}
			}
		}

		public override void OnResume ()
		{
			base.OnResume ();
			mapFragment.OnResume ();
			streetViewFragment.OnResume ();
		}

		public override void OnLowMemory ()
		{
			base.OnLowMemory ();
			mapFragment.OnLowMemory ();
			streetViewFragment.OnLowMemory ();
		}

		public override void OnPause ()
		{
			base.OnPause ();
			mapFragment.OnPause ();
			if (map != null)
				PreviousCameraPosition = map.CameraPosition;
			streetViewFragment.OnPause ();
		}

		public override void OnDestroy ()
		{
			base.OnDestroy ();
			mapFragment.OnDestroy ();
			streetViewFragment.OnDestroy ();
		}

		public override void OnSaveInstanceState (Bundle outState)
		{
			base.OnSaveInstanceState (outState);
			mapFragment.OnSaveInstanceState (outState);
			streetViewFragment.OnSaveInstanceState (outState);
		}

		void HandleMapClick (object sender, GoogleMap.MapClickEventArgs e)
		{
			currentShownID = -1;
			currentShownMarker = null;
			pane.SetState (InfoPane.State.Closed);
		}
		
		void HandleMarkerClick (object sender, GoogleMap.MarkerClickEventArgs e)
		{
			e.Handled = true;
			OpenStationWithMarker (e.Marker);
		}

		void HandleStarButtonChecked (object sender, EventArgs e)
		{
			if (currentShownID == -1)
				return;
			var favorites = favManager.LastFavorites ?? favManager.GetFavoriteStationIds ();
			bool contained = favorites.Contains (currentShownID);
			if (contained) {
				favManager.RemoveFromFavorite (currentShownID);
			} else {
				favManager.AddToFavorite (currentShownID);
			}
		}

		public async void FillUpMap (bool forceRefresh)
		{
			if (loading)
				return;
			loading = true;
			if (pane != null && pane.Opened)
				pane.SetState (InfoPane.State.Closed, animated: false);
			flashBar.ShowLoading ();

			try {
				var stations = await hubway.GetStations (forceRefresh);
				await SetMapStationPins (stations);
				lastUpdateText.Text = "Last refreshed: " + DateTime.Now.ToShortTimeString ();
			} catch (Exception e) {
				AnalyticsHelper.LogException ("DataFetcher", e);
				Android.Util.Log.Debug ("DataFetcher", e.ToString ());
			}

			flashBar.ShowLoaded ();
			showedStale = false;
			if (pendingSearchTerm != null) {
				OpenStationWithTerm (pendingSearchTerm);
				pendingSearchTerm = null;
			}
			loading = false;
		}

		async Task SetMapStationPins (Station[] stations, float alpha = 1)
		{
			var stationsToUpdate = stations.Where (station => {
				Marker marker;
				var stats = station.BikeCount + "|" + station.EmptySlotCount;
				if (existingMarkers.TryGetValue (station.Id, out marker)) {
					if (marker.Snippet == stats && !showedStale)
						return false;
					marker.Remove ();
				}
				return true;
			}).ToArray ();

			var pins = await Task.Run (() => stationsToUpdate.ToDictionary (station => station.Id, station => {
				var w = 24.ToPixels ();
				var h = 40.ToPixels ();
				if (station.Locked)
					return pinFactory.GetClosedPin (w, h);
				var ratio = (float)TruncateDigit (station.BikeCount / ((float)station.Capacity), 2);
				return pinFactory.GetPin (ratio,
				                          station.BikeCount,
				                          w, h,
				                          alpha: alpha);
			}));

			foreach (var station in stationsToUpdate) {
				var pin = pins [station.Id];

				var markerOptions = new MarkerOptions ()
					.SetTitle (station.Id + "|" + station.Name)
					.SetSnippet (station.Locked ? string.Empty : station.BikeCount + "|" + station.EmptySlotCount)
					.SetPosition (new Android.Gms.Maps.Model.LatLng (station.Location.Lat, station.Location.Lon))
					.SetIcon (BitmapDescriptorFactory.FromBitmap (pin));
				existingMarkers [station.Id] = map.AddMarker (markerOptions);
			}
		}

		public void CenterAndOpenStationOnMap (long id,
		                                       float zoom = 13,
		                                       int animDurationID = Android.Resource.Integer.ConfigShortAnimTime)
		{
			Marker marker;
			if (!existingMarkers.TryGetValue ((int)id, out marker))
				return;
			CenterAndOpenStationOnMap (marker, zoom, animDurationID);
		}

		public void CenterAndOpenStationOnMap (Marker marker,
		                                       float zoom = 13,
		                                       int animDurationID = Android.Resource.Integer.ConfigShortAnimTime)
		{
			var latLng = marker.Position;
			var camera = CameraUpdateFactory.NewLatLngZoom (latLng, zoom);
			var time = Resources.GetInteger (animDurationID);
			if (map != null)
				map.AnimateCamera (camera, time, new MapAnimCallback (() => OpenStationWithMarker (marker)));
		}

		public void OpenStationWithMarker (Marker marker)
		{
			if (string.IsNullOrEmpty (marker.Title) || marker.Title == SearchPinId)
				return;

			var name = pane.FindViewById<TextView> (Resource.Id.InfoViewName);
			var name2 = pane.FindViewById<TextView> (Resource.Id.InfoViewSecondName);
			var bikes = pane.FindViewById<TextView> (Resource.Id.InfoViewBikeNumber);
			var slots = pane.FindViewById<TextView> (Resource.Id.InfoViewSlotNumber);

			var splitTitle = marker.Title.Split ('|');
			string displayNameSecond;
			var displayName = StationUtils.CutStationName (splitTitle [1], out displayNameSecond);
			name.Text = displayName;
			name2.Text = displayNameSecond;

			currentShownID = int.Parse (splitTitle [0]);
			currentShownMarker = marker;

			var isLocked = string.IsNullOrEmpty (marker.Snippet);
			pane.FindViewById (Resource.Id.stationStats).Visibility = isLocked ? ViewStates.Gone : ViewStates.Visible;
			pane.FindViewById (Resource.Id.stationLock).Visibility = isLocked ? ViewStates.Visible : ViewStates.Gone;

			if (!isLocked) {
				var splitNumbers = marker.Snippet.Split ('|');
				bikes.Text = splitNumbers [0];
				slots.Text = splitNumbers [1];

				var baseGreen = Color.Rgb (0x66, 0x99, 0x00);
				var baseRed = Color.Rgb (0xcc, 0x00, 0x00);
				var bikesNum = int.Parse (splitNumbers [0]);
				var slotsNum = int.Parse (splitNumbers [1]);
				var total = bikesNum + slotsNum;
				var bikesColor = PinFactory.InterpolateColor (baseRed, baseGreen,
				                                              ((float)bikesNum) / total);
				var slotsColor = PinFactory.InterpolateColor (baseRed, baseGreen,
				                                              ((float)slotsNum) / total);
				bikes.SetTextColor (bikesColor);
				slots.SetTextColor (slotsColor);

				DrawableCompat.SetTint (bikes.GetCompoundDrawables ().Last (),
				                        bikesColor.ToArgb ());
				DrawableCompat.SetTint (slots.GetCompoundDrawables ().Last (),
				                        slotsColor.ToArgb ());
			}

			var favs = favManager.LastFavorites ?? favManager.GetFavoriteStationIds ();
			var starButton = pane.FindViewById<CheckedImageButton> (Resource.Id.StarButton);
			bool activated = favs.Contains (currentShownID);
			if (!AndroidExtensions.IsMaterial) {
				var favIcon = DrawableCompat.Wrap (Resources.GetDrawable (Resource.Drawable.ic_favorite));
				DrawableCompat.SetTintList (favIcon, Resources.GetColorStateList (Resource.Color.favorite_color_list));
				starButton.SetImageDrawable (favIcon);
			} else {
				starButton.SetImageResource (Resource.Drawable.ic_favorite_selector);
			}
			starButton.Checked = activated;
			starButton.Drawable.JumpToCurrentState ();

			if (streetPanorama != null) {
				streetPanorama.SetPosition (marker.Position);
				streetViewFragment.BringToFront ();
				streetViewFragment.Invalidate ();
			}

			LoadStationHistory (currentShownID);

			pane.SetState (InfoPane.State.Opened);
		}

		async void LoadStationHistory (int stationID)
		{
			const char DownArrow = '↘';
			const char UpArrow = '↗';

			var historyTimes = new int[] {
				Resource.Id.historyTime1,
				Resource.Id.historyTime2,
				Resource.Id.historyTime3,
				Resource.Id.historyTime4,
				Resource.Id.historyTime5
			};
			var historyValues = new int[] {
				Resource.Id.historyValue1,
				Resource.Id.historyValue2,
				Resource.Id.historyValue3,
				Resource.Id.historyValue4,
				Resource.Id.historyValue5
			};

			foreach (var ht in historyTimes)
				pane.FindViewById<TextView> (ht).Text = "-:-";
			foreach (var hv in historyValues) {
				var v = pane.FindViewById<TextView> (hv);
				v.Text = "-";
				v.SetTextColor (Color.Rgb (0x90, 0x90, 0x90));
			}
			var history = (await hubwayHistory.GetStationHistory (stationID)).ToArray ();
			if (stationID != currentShownID || history.Length == 0)
				return;

			var previousValue = history [0].Value;
			for (int i = 0; i < Math.Min (historyTimes.Length, history.Length - 1); i++) {
				var h = history [i + 1];

				var timeText = pane.FindViewById<TextView> (historyTimes [i]);
				var is24 = Android.Text.Format.DateFormat.Is24HourFormat (Activity);
				timeText.Text = h.Key.ToLocalTime ().ToString ((is24 ? "HH" : "hh") + ":mm");

				var valueText = pane.FindViewById<TextView> (historyValues [i]);
				var comparison = h.Value.CompareTo (previousValue);
				if (comparison == 0) {
					valueText.Text = "=";
				} else if (comparison > 0) {
					valueText.Text = (h.Value - previousValue).ToString () + UpArrow;
					valueText.SetTextColor (Color.Rgb (0x66, 0x99, 0x00));
				} else {
					valueText.Text = (previousValue - h.Value).ToString () + DownArrow;
					valueText.SetTextColor (Color.Rgb (0xcc, 00, 00));
				}
				previousValue = h.Value;
			}
		}

		public void CenterMapOnLocation (LatLng latLng)
		{
			if (map == null)
				return;
			var camera = CameraUpdateFactory.NewLatLngZoom (latLng, 16);
			map.AnimateCamera (camera,
			                   new MapAnimCallback (() => SetLocationPin (latLng)));
		}

		public void OnSearchIntent (Intent intent)
		{
			if (searchItem != null)
				searchItem.CollapseActionView ();

			// Either we are getting a lat/lng from an action bar search
			var serial = (string)intent.GetStringExtra (SearchManager.ExtraDataKey);
			// Or it comes from a general search
			var searchTerm = (string)intent.GetStringExtra (SearchManager.Query);

			if (serial != null) {
				var latlng = serial.Split ('|');
				var finalLatLng = new LatLng (latlng [0].ToSafeDouble (),
				                              latlng [1].ToSafeDouble ());
				CenterMapOnLocation (finalLatLng);
			} else if (!string.IsNullOrEmpty (searchTerm)) {
				if (existingMarkers.Count == 0)
					pendingSearchTerm = searchTerm;
				else
					OpenStationWithTerm (searchTerm);
			}
		}

		async void OpenStationWithTerm (string term)
		{
			try {
				var stations = await hubway.GetStations ();
				var bestResult = stations
					.Select (s => new { Station = s, Score = FuzzyStringMatch (term, s.Name) })
					.Where (s => s.Score > 0.5f)
					.OrderByDescending (s => s.Score)
					.FirstOrDefault ();
				if (bestResult == null) {
					Toast.MakeText (Activity, "No station found for '" + term + "'", ToastLength.Short).Show ();
					return;
				}
				CenterAndOpenStationOnMap (bestResult.Station.Id);
			} catch (Exception e) {
				e.Data ["Term"] = term;
				AnalyticsHelper.LogException ("TermStationSearch", e);
			}
		}

		float FuzzyStringMatch (string target, string other)
		{
			if (other.StartsWith (target, StringComparison.OrdinalIgnoreCase))
				return 2f;
			return LevenshteinDistance (target, other);
		}

		// Taken from Lucene.NET
		// See https://git-wip-us.apache.org/repos/asf?p=lucenenet.git;a=blob;f=NOTICE.txt
		public float LevenshteinDistance (string target, string other)
		{
			char[] sa;
			int n;
			int[] p, d, _d;

			sa = target.ToCharArray();
			n = sa.Length;
			p = new int[n + 1];
			d = new int[n + 1];
			int m = other.Length;

			if (n == 0 || m == 0)
				return n == m ? 1 : 0;

			int i;
			int j;
			char t_j;
			int cost;

			for (i = 0; i <= n; i++)
				p[i] = i;

			for (j = 1; j <= m; j++) {
				t_j = other[j - 1];
				d[0] = j;

				for (i = 1; i <= n; i++) {
					cost = sa[i - 1] == t_j ? 0 : 1;
					d[i] = Math.Min(Math.Min(d[i - 1] + 1, p[i] + 1), p[i - 1] + cost);
				}

				_d = p;
				p = d;
				d = _d;
			}

			return 1.0f - ((float)p[n] / Math.Max(other.Length, sa.Length));
		}

		CameraPosition PreviousCameraPosition {
			get {
				var prefs = Activity.GetPreferences (FileCreationMode.Private);
				if (!prefs.Contains ("lastPosition-bearing")
				    || !prefs.Contains ("lastPosition-tilt")
				    || !prefs.Contains ("lastPosition-zoom")
				    || !prefs.Contains ("lastPosition-lat")
				    || !prefs.Contains ("lastPosition-lon"))
					return null;

				var bearing = prefs.GetFloat ("lastPosition-bearing", 0);
				var tilt = prefs.GetFloat ("lastPosition-tilt", 0);
				var zoom = prefs.GetFloat ("lastPosition-zoom", 0);
				var latitude = prefs.GetFloat ("lastPosition-lat", 0);
				var longitude = prefs.GetFloat ("lastPosition-lon", 0);

				return new CameraPosition.Builder ()
					.Bearing (bearing)
					.Tilt (tilt)
					.Zoom (zoom)
					.Target (new LatLng (latitude, longitude))
					.Build ();
			}
			set {
				var position = map.CameraPosition;
				var prefs = Activity.GetPreferences (FileCreationMode.Private);
				using (var editor = prefs.Edit ()) {
					editor.PutFloat ("lastPosition-bearing", position.Bearing);
					editor.PutFloat ("lastPosition-tilt", position.Tilt);
					editor.PutFloat ("lastPosition-zoom", position.Zoom);
					editor.PutFloat ("lastPosition-lat", (float)position.Target.Latitude);
					editor.PutFloat ("lastPosition-lon", (float)position.Target.Longitude);
					editor.Commit ();
				}
			}
		}

		bool CenterMapOnUser ()
		{
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
			return true;
		}

		void SetLocationPin (LatLng finalLatLng)
		{
			if (locationPin != null) {
				locationPin.Remove ();
				locationPin = null;
			}
			var proj = map.Projection;
			var location = proj.ToScreenLocation (finalLatLng);
			location.Offset (0, -(35.ToPixels ()));
			var startLatLng = proj.FromScreenLocation (location);

			new Handler (Activity.MainLooper).PostDelayed (() => {
				var opts = new MarkerOptions ()
					.SetPosition (startLatLng)
					.SetTitle (SearchPinId)
					.SetIcon (BitmapDescriptorFactory.DefaultMarker (BitmapDescriptorFactory.HueViolet));
				var marker = map.AddMarker (opts);
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
	}
}

