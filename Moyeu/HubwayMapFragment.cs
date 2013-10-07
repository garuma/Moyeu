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

using XamSvg;

namespace Moyeu
{
	public class HubwayMapFragment : Android.Support.V4.App.Fragment, ViewTreeObserver.IOnGlobalLayoutListener, IMoyeuSection
	{
		Dictionary<int, Marker> existingMarkers = new Dictionary<int, Marker> ();
		Marker locationPin;
		MapView mapFragment;
		Hubway hubway = Hubway.Instance;

		bool loading;
		bool showedStale;
		FlashBarController flashBar;
		IMenuItem searchItem;
		FavoriteManager favManager;
		TextView lastUpdateText;
		PinFactory pinFactory;
		InfoPane pane;
		PictureBitmapDrawable starOnDrawable;
		PictureBitmapDrawable starOffDrawable;

		int currentShownID = -1;
		Marker currentShownMarker;
		CameraPosition oldPosition;

		public HubwayMapFragment (Context context)
		{
			MapsInitializer.Initialize (context);
			this.pinFactory = new PinFactory (context);
			this.favManager = FavoriteManager.Obtain (context);
			SetHasOptionsMenu (true);
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

		public override void OnStart ()
		{
			base.OnStart ();
			RefreshData ();
		}

		public void OnGlobalLayout ()
		{
			Activity.RunOnUiThread (() => {
				var svImgContainer = (FrameLayout)pane.FindViewById<ImageView> (Resource.Id.streetViewImg).Parent;
				if (svImgContainer.Height > GoogleApis.MaxStreetViewSize
				    || svImgContainer.Width > GoogleApis.MaxStreetViewSize) {
					var param = svImgContainer.LayoutParameters;
					param.Height = Math.Min (GoogleApis.MaxStreetViewSize, svImgContainer.Height);
					param.Width = Math.Min (GoogleApis.MaxStreetViewSize, svImgContainer.Width);
					svImgContainer.LayoutParameters = param;
				}
				pane.SetState (InfoPane.State.Closed, animated: false);
			});
			View.ViewTreeObserver.RemoveGlobalOnLayoutListener (this);
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			var view = inflater.Inflate (Resource.Layout.MapLayout, container, false);
			mapFragment = view.FindViewById<MapView> (Resource.Id.map);
			mapFragment.OnCreate (savedInstanceState);
			lastUpdateText = view.FindViewById<TextView> (Resource.Id.UpdateTimeText);
			pane = view.FindViewById<InfoPane> (Resource.Id.infoPane);
			pane.StateChanged += HandlePaneStateChanged;
			view.ViewTreeObserver.AddOnGlobalLayoutListener (this);
			flashBar = new FlashBarController (view);
			return view;
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);
			mapFragment.Map.MyLocationEnabled = true;
			mapFragment.Map.UiSettings.MyLocationButtonEnabled = false;
			mapFragment.Map.MarkerClick += HandleMarkerClick;
			mapFragment.Map.MapClick += HandleMapClick;
			var oldPosition = PreviousCameraPosition;
			if (oldPosition != null)
				mapFragment.Map.MoveCamera (CameraUpdateFactory.NewCameraPosition (oldPosition));

			// Setup info pane
			var bikeDrawable = SvgFactory.GetDrawable (Resources, Resource.Raw.bike);
			var lockDrawable = SvgFactory.GetDrawable (Resources, Resource.Raw.ic_lock);
			var mapDrawable = SvgFactory.GetDrawable (Resources, Resource.Raw.map);
			starOnDrawable = SvgFactory.GetDrawable (Resources, Resource.Raw.star_on);
			starOffDrawable = SvgFactory.GetDrawable (Resources, Resource.Raw.star_off);
			pane.FindViewById<ImageView> (Resource.Id.bikeImageView).SetImageDrawable (bikeDrawable);
			pane.FindViewById<ImageView> (Resource.Id.lockImageView).SetImageDrawable (lockDrawable);
			var starBtn = pane.FindViewById<ImageButton> (Resource.Id.StarButton);
			starBtn.Click += HandleStarButtonChecked;
			var mapBtn = pane.FindViewById<Button> (Resource.Id.gmapsBtn);
			mapDrawable.SetBounds (0, 0, 32.ToPixels (), 32.ToPixels ());
			mapBtn.SetCompoundDrawablesRelative (mapDrawable, null, null, null);
			mapBtn.CompoundDrawablePadding = 6.ToPixels ();
			mapBtn.Click += HandleMapButtonClick;
		}

		void HandlePaneStateChanged (InfoPane.State state)
		{
			var time = Resources.GetInteger (Android.Resource.Integer.ConfigShortAnimTime);
			var enabled = state != InfoPane.State.FullyOpened;
			mapFragment.Map.UiSettings.ScrollGesturesEnabled = enabled;
			mapFragment.Map.UiSettings.ZoomGesturesEnabled = enabled;
			if (state == InfoPane.State.FullyOpened && currentShownMarker != null) {
				oldPosition = mapFragment.Map.CameraPosition;
				var destX = mapFragment.Width / 2;
				var destY = (mapFragment.Height - pane.Height) / 2;
				var currentPoint = mapFragment.Map.Projection.ToScreenLocation (currentShownMarker.Position);
				var scroll = CameraUpdateFactory.ScrollBy (- destX + currentPoint.X, - destY + currentPoint.Y);
				mapFragment.Map.AnimateCamera (scroll, time, null);
			} else if (oldPosition != null) {
				mapFragment.Map.AnimateCamera (CameraUpdateFactory.NewCameraPosition (oldPosition), time, null);
				oldPosition = null;
			}
		}

		void HandleMapButtonClick (object sender, EventArgs e)
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
			SetupSearchInput ((SearchView)searchItem.ActionView);
		}

		void SetupSearchInput (SearchView searchView)
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
				if (pos != null) {
					var update = CameraUpdateFactory.NewCameraPosition (pos);
					mapFragment.Map.MoveCamera (update);
				}
			}
		}

		public override void OnResume ()
		{
			base.OnResume ();
			mapFragment.OnResume ();
		}

		public override void OnLowMemory ()
		{
			base.OnLowMemory ();
			mapFragment.OnLowMemory ();
		}

		public override void OnPause ()
		{
			base.OnPause ();
			mapFragment.OnPause ();
			PreviousCameraPosition = mapFragment.Map.CameraPosition;
		}

		public override void OnDestroy ()
		{
			base.OnDestroy ();
			mapFragment.OnDestroy ();
		}

		public override void OnSaveInstanceState (Bundle outState)
		{
			base.OnSaveInstanceState (outState);
			mapFragment.OnSaveInstanceState (outState);
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
			OpenStationWithMarker (e.P0);
		}

		void HandleStarButtonChecked (object sender, EventArgs e)
		{
			if (currentShownID == -1)
				return;
			var starButton = (ImageButton)sender;
			var favorites = favManager.LastFavorites ?? favManager.GetFavoriteStationIds ();
			bool contained = favorites.Contains (currentShownID);
			if (contained) {
				starButton.SetImageDrawable (starOffDrawable);
				favManager.RemoveFromFavorite (currentShownID);
			} else {
				starButton.SetImageDrawable (starOnDrawable);
				favManager.AddToFavorite (currentShownID);
			}
		}

		public async void FillUpMap (bool forceRefresh)
		{
			if (loading)
				return;
			loading = true;
			if (pane != null && pane.Opened)
				pane.SetState (InfoPane.State.Closed);
			flashBar.ShowLoading ();

			try {
				var stations = await hubway.GetStations (forceRefresh);
				await SetMapStationPins (stations);
				lastUpdateText.Text = "Last refreshed: " + DateTime.Now.ToShortTimeString ();
			} catch (Exception e) {
				Android.Util.Log.Debug ("DataFetcher", e.ToString ());
			}

			flashBar.ShowLoaded ();
			showedStale = false;
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
				var ratio = (float)TruncateDigit (station.BikeCount / ((float)station.Capacity), 2);
				return pinFactory.GetPin (ratio,
				                          station.BikeCount,
				                          24.ToPixels (), 40.ToPixels (),
				                          alpha: alpha);
			}));

			foreach (var station in stationsToUpdate) {
				var pin = pins [station.Id];

				var markerOptions = new MarkerOptions ()
					.SetTitle (station.Id + "|" + station.Name)
					.SetSnippet (station.BikeCount + "|" + station.EmptySlotCount)
					.SetPosition (new Android.Gms.Maps.Model.LatLng (station.Location.Lat, station.Location.Lon))
					.InvokeIcon (BitmapDescriptorFactory.FromBitmap (pin));
				existingMarkers [station.Id] = mapFragment.Map.AddMarker (markerOptions);
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
			mapFragment.Map.AnimateCamera (camera, time, new MapAnimCallback (() => OpenStationWithMarker (marker)));
		}

		public void OpenStationWithMarker (Marker marker)
		{
			var name = pane.FindViewById<TextView> (Resource.Id.InfoViewName);
			var name2 = pane.FindViewById<TextView> (Resource.Id.InfoViewSecondName);
			var bikes = pane.FindViewById<TextView> (Resource.Id.InfoViewBikeNumber);
			var slots = pane.FindViewById<TextView> (Resource.Id.InfoViewSlotNumber);
			var starButton = pane.FindViewById<ImageButton> (Resource.Id.StarButton);
			var svSpinner = pane.FindViewById<ProgressBar> (Resource.Id.streetViewSpinner);
			var svImg = pane.FindViewById<ImageView> (Resource.Id.streetViewImg);

			var splitTitle = marker.Title.Split ('|');
			string displayNameSecond;
			var displayName = Hubway.CutStationName (splitTitle [1], out displayNameSecond);
			name.Text = displayName;
			name2.Text = displayNameSecond;

			currentShownID = int.Parse (splitTitle [0]);
			currentShownMarker = marker;

			var splitNumbers = marker.Snippet.Split ('|');
			bikes.Text = splitNumbers [0];
			slots.Text = splitNumbers [1];

			var favs = favManager.LastFavorites ?? favManager.GetFavoriteStationIds ();
			bool activated = favs.Contains (currentShownID);
			starButton.SetImageDrawable (activated ? starOnDrawable : starOffDrawable);

			var api = GoogleApis.Obtain (Activity);
			string mapUrl = GoogleApis.MakeStreetViewUrl (svImg, marker.Position);
			Bitmap mapBmp = null;
			if (api.StreetViewCache.TryGet (mapUrl, out mapBmp)) {
				svImg.Alpha = 1;
				svImg.Visibility = ViewStates.Visible;
				svSpinner.Visibility = ViewStates.Gone;
				svImg.SetImageDrawable (new RoundCornerDrawable (mapBmp));
			} else {
				svImg.Alpha = 0;
				svImg.Visibility = ViewStates.Invisible;
				svSpinner.Visibility = ViewStates.Visible;
				svSpinner.Alpha = 1;
				api.LoadStreetView (marker.Position, this, currentShownID, svSpinner, svImg);
			}

			pane.SetState (InfoPane.State.Opened);
		}

		public void CenterMapOnLocation (LatLng latLng)
		{
			var camera = CameraUpdateFactory.NewLatLngZoom (latLng, 16);
			mapFragment.Map.AnimateCamera (camera,
			                               new MapAnimCallback (() => SetLocationPin (latLng)));
		}

		public void OnSearchIntent (Intent intent)
		{
			searchItem.CollapseActionView ();
			if (intent.Action != Intent.ActionSearch)
				return;
			var serial = (string)intent.Extras.Get (SearchManager.ExtraDataKey);
			if (serial == null)
				return;
			var latlng = serial.Split ('|');
			var finalLatLng = new LatLng (double.Parse (latlng[0]),
			                              double.Parse (latlng[1]));
			CenterMapOnLocation (finalLatLng);
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
				var position = mapFragment.Map.CameraPosition;
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
			return true;
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

			new Handler (Activity.MainLooper).PostDelayed (() => {
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
	}
}

