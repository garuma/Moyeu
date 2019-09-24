using System;
using System.Collections.Generic;
using System.Linq;
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
using Android.Animation;
using Android.Util;

using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Gms.Location.Places.UI;
using Com.Google.Maps.Android.Clustering;
using Android.Support.V4.Graphics.Drawable;
using Android.Support.Design.Widget;
using ResCompat = Android.Support.V4.Content.Res.ResourcesCompat;
using ContextCompat = Android.Support.V4.Content.ContextCompat;
using Neteril.Android;

namespace Moyeu
{
	public class HubwayMapFragment: Android.Support.V4.App.Fragment, ViewTreeObserver.IOnGlobalLayoutListener, IMoyeuSection, IOnMapReadyCallback, IOnStreetViewPanoramaReadyCallback
	{
		Dictionary<int, ClusterMarkerOptions> existingMarkers = new Dictionary<int, ClusterMarkerOptions> ();
		Marker locationPin;
		MapView mapFragment;
		GoogleMap map;
		StreetViewPanoramaView streetViewFragment;
		StreetViewPanorama streetPanorama;
		ClusterManager clusterManager;
		Hubway hubway = Hubway.Instance;
		HubwayHistory hubwayHistory = new HubwayHistory ();

		bool loading;
		bool showedStale;
		string pendingSearchTerm;
		FlashBarController flashBar;
		FavoriteManager favManager;
		TextView lastUpdateText;
		PinFactory pinFactory;
		InfoPane pane;
		SwitchableFab fab;

		const int SearchRequestID = 1532;
		const string SearchPinId = "SEARCH_PIN";
		MarkerOptions currentShownMarker;
		CameraPosition oldPosition;

		// Info pane views
		TextView ipName, ipName2, ipBikes, ipSlots, ipDistance;
		View ipStationLock;
		ImageView ipSlotsImg, ipBikesImg;
		Drawable bikeDrawable, rackDrawable;

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

		public bool OnBackPressed ()
		{
			if (pane != null && pane.Opened) {
				pane.SetState (InfoPane.State.Closed);
				return true;
			}
			return false;
		}

		internal int CurrentShownId { get; private set; } = -1;

		public void RefreshData ()
		{
			FillUpMap (forceRefresh: false);
		}

		public override void OnActivityCreated (Bundle savedInstanceState)
		{
			base.OnActivityCreated (savedInstanceState);

			var context = Activity;
			pinFactory = new PinFactory (context);
			favManager = FavoriteManager.Obtain (context);
		}

		public override void OnStart ()
		{
			base.OnStart ();
			RefreshData ();
		}

		public void OnGlobalLayout ()
		{
			Activity.RunOnUiThread (() => pane.SetState (InfoPane.State.Closed, animated: false));
			View.ViewTreeObserver.RemoveOnGlobalLayoutListener (this);
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

			return view;
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

			mapFragment.GetMapAsync (this);

			// Setup info pane
			ipName = pane.FindViewById<TextView> (Resource.Id.InfoViewName);
			ipName2 = pane.FindViewById<TextView> (Resource.Id.InfoViewSecondName);
			ipBikes = pane.FindViewById<TextView> (Resource.Id.InfoViewBikeNumber);
			ipSlots = pane.FindViewById<TextView> (Resource.Id.InfoViewSlotNumber);
			ipDistance = pane.FindViewById<TextView> (Resource.Id.InfoViewDistance);
			ipBikesImg = pane.FindViewById<ImageView> (Resource.Id.InfoViewBikeNumberImg);
			ipSlotsImg = pane.FindViewById<ImageView> (Resource.Id.InfoViewSlotNumberImg);

			bikeDrawable = ResCompat.GetDrawable (Resources, Resource.Drawable.ic_bike_vector, null);
			rackDrawable = ResCompat.GetDrawable (Resources, Resource.Drawable.ic_racks_vector, null);

			ipBikesImg.SetImageDrawable (bikeDrawable);
			ipSlotsImg.SetImageDrawable (rackDrawable);

			streetViewFragment.GetStreetViewPanoramaAsync (this);

			fab = view.FindViewById<SwitchableFab> (Resource.Id.fabButton);
			fab.Click += HandleFabClicked;
		}

		public void OnMapReady (GoogleMap googleMap)
		{
			map = googleMap;
			MapsInitializer.Initialize (Activity.ApplicationContext);
			clusterManager = new ClusterManager (Context, map);
			clusterManager.Renderer = new MoyeuClusterRenderer (Context, map, clusterManager);
			map.SetOnCameraIdleListener (clusterManager);

			// Default map initialization
			if (ContextCompat.CheckSelfPermission (Activity, Android.Manifest.Permission.AccessFineLocation) == Permission.Granted)
				googleMap.MyLocationEnabled = true;
			googleMap.UiSettings.MyLocationButtonEnabled = false;

			googleMap.SetMapStyle (MapStyleOptions.LoadRawResourceStyle (Context, Resource.Raw.GMapStyle));

			googleMap.MarkerClick += HandleMarkerClick;
			googleMap.MapClick += HandleMapClick;
			var oldPosition = PreviousCameraPosition;
			if (oldPosition != null)
				googleMap.MoveCamera (CameraUpdateFactory.NewCameraPosition (oldPosition));
		}

		public override void OnRequestPermissionsResult (int requestCode, string[] permissions, Permission[] grantResults)
		{
			if (map != null && Enumerable.Range (0, permissions.Length).Any (i => permissions [i] == Android.Manifest.Permission.AccessFineLocation
																			 && grantResults [i] == Permission.Granted)) {
				map.MyLocationEnabled = true;
			}
		}

		public void OnStreetViewPanoramaReady (StreetViewPanorama panorama)
		{
			streetPanorama = panorama;
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
			if (stations == null || CurrentShownId == -1)
				return;

			var stationIndex = Array.FindIndex (stations, s => s.Id == CurrentShownId);
			if (stationIndex == -1)
				return;
			var station = stations [stationIndex];
			var location = station.GeoUrl;
			var uri = Android.Net.Uri.Parse (location);
			var intent = new Intent (Intent.ActionView, uri);
			StartActivity (intent);
		}

		void HandleFabClicked (object sender, EventArgs e)
		{
			if (pane.Opened)
				HandleStarButtonChecked (sender, e);
			else
				CenterMapOnUser ().IgnoreIfFaulted ();
		}

		public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
		{
			inflater.Inflate (Resource.Menu.map_menu, menu);
		}


		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			if (item.ItemId == Resource.Id.menu_refresh) {
				FillUpMap (forceRefresh: true);
				return true;
			} else if (item.ItemId == Resource.Id.menu_search) {
				LaunchPlaceSearch ();
			}
			return base.OnOptionsItemSelected (item);
		}

		void LaunchPlaceSearch ()
		{
			const double LowerLeftLat = 42.343828;
			const double LowerLeftLon = -71.169777;
			const double UpperRightLat = 42.401150;
			const double UpperRightLon = -71.017685;

			try {
				var intent = new PlaceAutocomplete.IntentBuilder (PlaceAutocomplete.ModeFullscreen)
												  .SetBoundsBias (new LatLngBounds (new LatLng (LowerLeftLat, LowerLeftLon),
																					new LatLng (UpperRightLat, UpperRightLon)))
												  .Build (Activity);
				StartActivityForResult (intent, SearchRequestID);
			} catch (Exception e) {
				AnalyticsHelper.LogException ("PlaceSearch", e);
				Log.Debug ("PlaceSearch", e.ToString ());
			}
		}

		public override void OnActivityResult (int requestCode, int resultCode, Intent data)
		{
			if (requestCode == SearchRequestID && resultCode == (int)Result.Ok) {
				var place = PlaceAutocomplete.GetPlace (Context, data);
				CenterMapOnLocation (place.LatLng);
			}
			base.OnActivityResult (requestCode, resultCode, data);
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
			CurrentShownId = -1;
			currentShownMarker = null;
			pane.SetState (InfoPane.State.Closed);
		}
		
		void HandleMarkerClick (object sender, GoogleMap.MarkerClickEventArgs e)
		{
			e.Handled = true;
			var marker = e.Marker;
			using (var markerOptions = new MarkerOptions ()
			       .SetTitle (marker.Title)
			       .SetSnippet (marker.Title)
			       .SetPosition (marker.Position))
				OpenStationWithMarker (markerOptions).IgnoreIfFaulted ("Couldn't open station");
		}

		void HandleStarButtonChecked (object sender, EventArgs e)
		{
			if (CurrentShownId == -1)
				return;
			var favorites = favManager.LastFavorites ?? favManager.GetFavoriteStationIds ();
			bool contained = favorites.Contains (CurrentShownId);
			if (contained) {
				favManager.RemoveFromFavorite (CurrentShownId);
			} else {
				favManager.AddToFavorite (CurrentShownId);
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

			using (var scope = ActivityScope.Of (Activity))
				await DoFillUpMap (scope, forceRefresh);

			flashBar.ShowLoaded ();
			showedStale = false;
			if (pendingSearchTerm != null) {
				OpenStationWithTerm (pendingSearchTerm);
				pendingSearchTerm = null;
			}
			loading = false;
		}

		async ActivityTask DoFillUpMap (ActivityScope scope, bool forceRefresh)
		{
			try {
				var stations = await hubway.GetStations (forceRefresh);
				await SetMapStationPins (stations);
				lastUpdateText.Text = "Last refreshed: " + DateTime.Now.ToShortTimeString ();
			} catch (Exception e) {
				AnalyticsHelper.LogException ("DataFetcher", e);
				Log.Debug ("DataFetcher", e.ToString ());
			}
		}

		async Task SetMapStationPins (Station[] stations, float alpha = 1)
		{
			var stationsToUpdate = stations.Where (station => {
				ClusterMarkerOptions marker;
				var stats = station.Locked ? string.Empty : station.BikeCount + "|" + station.EmptySlotCount;
				if (existingMarkers.TryGetValue (station.Id, out marker)) {
					if (marker.Options.Snippet == stats && !showedStale)
						return false;
					clusterManager.RemoveItem (marker);
				}
				return true;
			}).ToList ();

			var w = (int)Math.Round (TypedValue.ApplyDimension (ComplexUnitType.Dip, 32, Resources.DisplayMetrics));
			var h = (int)Math.Round (TypedValue.ApplyDimension (ComplexUnitType.Dip, 34, Resources.DisplayMetrics));

			var pins = await Task.Run (() => stationsToUpdate.ToDictionary (station => station.Id, station => {
				if (station.Locked)
					return BitmapDescriptorFactory.FromBitmap (pinFactory.GetClosedPin (w, h));
				var ratio = (float)TruncateDigit (station.BikeCount / ((float)station.Capacity), 2);
				return BitmapDescriptorFactory.FromFile (pinFactory.GetPin (ratio,
																			station.BikeCount,
																			w, h,
																			alpha: alpha));
			}));

			var clusterItems = new List<ClusterMarkerOptions> ();
			foreach (var station in stationsToUpdate) {
				var pin = pins [station.Id];

				var markerOptions = new MarkerOptions ()
					.SetTitle (station.Id + "|" + station.Name)
					.SetSnippet (station.Locked ? string.Empty : station.BikeCount + "|" + station.EmptySlotCount)
					.SetPosition (new LatLng (station.Location.Lat, station.Location.Lon))
					.SetIcon (pin);
				var markerItem = new ClusterMarkerOptions (markerOptions);
				existingMarkers [station.Id] = markerItem;
				clusterItems.Add (markerItem);
			}
			clusterManager.AddItems (clusterItems);
			clusterManager.Cluster ();
		}

		public void CenterAndOpenStationOnMap (long id,
		                                       float zoom = 13,
		                                       int animDurationID = Android.Resource.Integer.ConfigShortAnimTime)
		{
			ClusterMarkerOptions marker;
			if (!existingMarkers.TryGetValue ((int)id, out marker))
				return;
			CenterAndOpenStationOnMap (marker.Options, zoom, animDurationID);
		}

		public void CenterAndOpenStationOnMap (MarkerOptions marker,
		                                       float zoom = 13,
		                                       int animDurationID = Android.Resource.Integer.ConfigShortAnimTime)
		{
			var latLng = marker.Position;
			var camera = CameraUpdateFactory.NewLatLngZoom (latLng, zoom);
			var time = Resources.GetInteger (animDurationID);
			if (map != null)
				map.AnimateCamera (camera, time, new MapAnimCallback (() => OpenStationWithMarker (marker).IgnoreIfFaulted ("Couldn't open station")));
		}

		public async Task OpenStationWithMarker (MarkerOptions marker)
		{
			if (string.IsNullOrEmpty (marker.Title) || marker.Title == SearchPinId)
				return;

			var currentLocation = await ((MainActivity)Activity)?.GetCurrentUserLocationAsync ();

			var splitTitle = marker.Title.Split ('|');
			string displayNameSecond;
			var displayName = StationUtils.CutStationName (splitTitle [1], out displayNameSecond);
			ipName.Text = displayName;
			ipName2.Text = displayNameSecond;

			CurrentShownId = int.Parse (splitTitle [0]);
			currentShownMarker = marker;

			var isLocked = string.IsNullOrEmpty (marker.Snippet);
			if (ipStationLock == null)
				ipStationLock = pane.FindViewById<View> (Resource.Id.stationLock);
			ipStationLock.Visibility = isLocked ? ViewStates.Visible : ViewStates.Gone;

			if (!isLocked) {
				var splitNumbers = marker.Snippet.Split ('|');
				ipBikes.Text = splitNumbers [0];
				ipSlots.Text = splitNumbers [1];

				var baseGreen = Color.Rgb (0x66, 0x99, 0x00);
				var baseRed = Color.Rgb (0xcc, 0x00, 0x00);
				var bikesNum = int.Parse (splitNumbers [0]);
				var slotsNum = int.Parse (splitNumbers [1]);
				var total = bikesNum + slotsNum;

				double distance = double.NaN;
				if (currentLocation != null) {
					distance = GeoUtils.Distance (
						new GeoPoint { Lat = marker.Position.Latitude, Lon = marker.Position.Longitude },
						new GeoPoint { Lat = currentLocation.Latitude, Lon = currentLocation.Longitude }
					) * 1000;
				}
				var bikesColor = PinFactory.InterpolateColor (baseRed, baseGreen,
				                                              ((float)bikesNum) / total);
				var slotsColor = PinFactory.InterpolateColor (baseRed, baseGreen,
				                                              ((float)slotsNum) / total);
				ipBikes.SetTextColor (bikesColor);
				ipSlots.SetTextColor (slotsColor);

				DrawableCompat.SetTint (bikeDrawable,
				                        bikesColor.ToArgb ());
				DrawableCompat.SetTint (rackDrawable,
				                        slotsColor.ToArgb ());

				if (double.IsNaN (distance))
					ipDistance.Text = string.Empty;
				else
					ipDistance.Text = GeoUtils.GetDisplayDistance (distance)
						+ " " + GeoUtils.GetUnitForDistance (distance);

				ipDistance.Visibility = ViewStates.Visible;
				ipBikes.Visibility = ViewStates.Visible;
				ipSlots.Visibility = ViewStates.Visible;
				ipBikesImg.Visibility = ViewStates.Visible;
				ipSlotsImg.Visibility = ViewStates.Visible;
			} else {
				ipDistance.Visibility = ViewStates.Gone;
				ipBikes.Visibility = ViewStates.Invisible;
				ipSlots.Visibility = ViewStates.Invisible;
				ipBikesImg.Visibility = ViewStates.Invisible;
				ipSlotsImg.Visibility = ViewStates.Invisible;
			}

			var favs = favManager.LastFavorites ?? favManager.GetFavoriteStationIds ();
			bool activated = favs.Contains (CurrentShownId);
			fab.Checked = activated;
			fab.JumpDrawablesToCurrentState ();

			if (streetPanorama != null)
				streetPanorama.SetPosition (marker.Position);

			LoadStationHistory (CurrentShownId);

			pane.SetState (InfoPane.State.Opened);
		}

		async void LoadStationHistory (int stationID)
		{
			try {
				using (var scope = ActivityScope.Of (Activity))
					await DoLoadStationHistory (scope, stationID);
			} catch (Exception e) {
				AnalyticsHelper.LogException ("HistoryFetcher", e);
				Log.Debug ("HistoryFetcher", e.ToString ());
			}
		}

		async ActivityTask DoLoadStationHistory (ActivityScope scope, int stationID)
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
			var history = (await hubwayHistory.GetStationHistory (stationID)).ToList ();
			if (stationID != CurrentShownId || history.Count == 0)
				return;

			var previousValue = history [0].Value;
			for (int i = 0; i < Math.Min (historyTimes.Length, history.Count - 1); i++) {
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
			// Either we are getting a lat/lng from an action bar search
			var serial = intent.GetStringExtra (SearchManager.ExtraDataKey);
			// Or it comes from a general search
			var searchTerm = intent.GetStringExtra (SearchManager.Query);

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
				var position = value;
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

		async Task<bool> CenterMapOnUser ()
		{
			if (!map.MyLocationEnabled)
				return false;
			var location = await ((MainActivity)Activity)?.GetCurrentUserLocationAsync ();
			if (location == null)
				return false;
			var userPos = new LatLng (location.Latitude, location.Longitude);
			var camPos = map.CameraPosition.Target;
			var needZoom = ShallowDoubleEquals (TruncateDigit (camPos.Latitude, 4), TruncateDigit (userPos.Latitude, 4))
				&& ShallowDoubleEquals (TruncateDigit (camPos.Longitude, 4), TruncateDigit (userPos.Longitude, 4));
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
			location.Offset (0, -35.ToPixels ());
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
			readonly Action callback;

			public MapAnimCallback (Action callback)
			{
				this.callback = callback;
			}

			public void OnCancel ()
			{
			}

			public void OnFinish () => callback?.Invoke ();
		}

		double TruncateDigit (double d, int digitNumber)
		{
			var power = Math.Pow (10, digitNumber);
			return Math.Truncate (d * power) / power;
		}

		bool ShallowDoubleEquals (double d1, double d2)
		{
			return Math.Abs (d1 - d2) < double.Epsilon;
		}
	}

	public class InfoPaneFabBehavior : CoordinatorLayout.Behavior
	{
		int minMarginBottom;
		bool wasOpened = false;

		public InfoPaneFabBehavior (Context context, IAttributeSet attrs) : base (context, attrs)
		{
			minMarginBottom = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 16, context.Resources.DisplayMetrics);
		}

		public override bool LayoutDependsOn (CoordinatorLayout parent, Java.Lang.Object child, View dependency)
		{
			return dependency is InfoPane;
		}

		public override bool OnDependentViewChanged (CoordinatorLayout parent, Java.Lang.Object child, View dependency)
		{
			// Move the fab vertically to place correctly wrt the info pane
			var fab = child.JavaCast<SwitchableFab> ();
			var currentInfoPaneY = dependency.TranslationY;
			var newTransY = (int)Math.Max (0, dependency.Height - currentInfoPaneY - minMarginBottom - fab.Height / 2);
			fab.TranslationY = -newTransY;

			// If alternating between open/closed state, change the FAB face
			if (wasOpened ^ ((InfoPane)dependency).Opened) {
				fab.Switch ();
				wasOpened = !wasOpened;
			}

			return true;
		}
	}

	class ClusterMarkerOptions : Java.Lang.Object, IClusterItem
	{
		public MarkerOptions Options { get; }

		public LatLng Position => Options.Position;
		public string Snippet => Options.Snippet;
		public string Title => Options.Title;

		public ClusterMarkerOptions (MarkerOptions options)
		{
			Options = options;
		}
	}

	class MoyeuClusterRenderer : Com.Google.Maps.Android.Clustering.View.DefaultClusterRenderer
	{
		Context context;

		public MoyeuClusterRenderer (Context context, GoogleMap map, ClusterManager manager)
			: base (context, map, manager)
		{
			this.context = context;
		}

		protected override int GetColor (int clusterCount)
		{
			return context.GetColor (Resource.Color.moyeu_primary);
		}

		protected override void OnBeforeClusterItemRendered (Java.Lang.Object item, MarkerOptions options)
		{
			var marker = item.JavaCast<ClusterMarkerOptions> ();
			options
				.SetTitle (marker.Options.Title)
				.SetSnippet (marker.Options.Snippet)
				.SetIcon (marker.Options.Icon);
		}
	}
}

