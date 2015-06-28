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
using Android.Animation;
using Android.Graphics.Drawables;
using Android.Util;

using Android.Gms.Maps;
using Android.Gms.Maps.Model;

namespace Moyeu
{
	public class FavoriteFragment : Android.Support.V4.App.ListFragment, IMoyeuSection
	{
		FavoriteManager favManager;
		FavoriteAdapter adapter;
		bool refreshRequested;

		public FavoriteFragment ()
		{
			HasOptionsMenu = false;
			AnimationExtensions.SetupFragmentTransitions (this);
		}

		public override void OnActivityCreated (Bundle savedInstanceState)
		{
			base.OnActivityCreated (savedInstanceState);
			this.favManager = FavoriteManager.Obtain (Activity);
			ListAdapter = this.adapter = new FavoriteAdapter (Activity);

			if (refreshRequested) {
				refreshRequested = false;
				RefreshData ();
			}
		}

		public string Name {
			get {
				return "FavoriteFragment";
			}
		}

		public string Title {
			get {
				return "Favorites";
			}
		}

		public async void RefreshData ()
		{
			if (favManager == null) {
				refreshRequested = true;
				return;
			}

			var favs = favManager.LastFavorites
			           ?? (await favManager.GetFavoriteStationIdsAsync ());
			adapter.SetStationIds (favs);
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);
			view.Background = AndroidExtensions.DefaultBackground;
			SetEmptyText ("You have no favorites yet");
			ListView.ItemClick += (sender, e) => StationShower (e.Id);
			ListView.DividerHeight = 0;
			ListView.Divider = null;
			ListView.SetSelector (Android.Resource.Color.Transparent);
			ListView.Recycler += HandleListRecycling;
		}

		void HandleListRecycling (object sender, AbsListView.RecyclerEventArgs e)
		{
			var favView = e.View as FavoriteView;
			if (favView == null)
				return;
			favView.ClearMap ();
		}

		void StationShower (long id)
		{
			var navigator = Activity as MainActivity;
			if (navigator != null)
				navigator.SwitchToMapAndShowStationWithId (id);
		}
	}

	class FavoriteAdapter : BaseAdapter
	{
		List<Station> stations;
		Context context;

		public FavoriteAdapter (Context context)
		{
			this.context = context;
		}

		public async void SetStationIds (HashSet<int> ids)
		{
			while (true) {
				try {
					var hubway = Hubway.Instance;
					var stationList = hubway.HasCachedData ? hubway.LastStations : (await hubway.GetStations ());
					stations = stationList
						.Where (s => ids.Contains (s.Id))
						.OrderBy (s => s.Name)
						.ToList ();
				} catch (Exception e) {
					AnalyticsHelper.LogException ("Favorite", e);
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
			var view = convertView as FavoriteView;
			if (view == null) {
				view = new FavoriteView (context);
				view.InitializeMapView ();
			}

			var station = stations [position];
			view.Station = station;
			view.RefreshMapLocation ();

			string secondPart;
			view.StationName.Text = StationUtils.CutStationName (station.Name, out secondPart);
			view.SecondStationName.Text = secondPart;
			view.BikeNumber.Text = station.BikeCount.ToString ();
			view.SlotNumber.Text = station.Capacity.ToString ();

			return view;
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
	}

	class FavoriteView : FrameLayout, IOnMapReadyCallback
	{
		GoogleMap map;

		public FavoriteView (Context context) : base (context)
		{
			var inflater = LayoutInflater.From (context);
			inflater.Inflate (Resource.Layout.FavoriteItem, this, true);

			MapView = FindViewById<MapView> (Resource.Id.StationMap);
			StationName = FindViewById<TextView> (Resource.Id.MainStationName);
			SecondStationName = FindViewById<TextView> (Resource.Id.SecondStationName);
			BikeNumber = FindViewById<TextView> (Resource.Id.BikeNumber);
			SlotNumber = FindViewById<TextView> (Resource.Id.SlotNumber);
		}

		public void InitializeMapView ()
		{
			MapView.OnCreate (null);
			MapView.GetMapAsync (this);
		}

		public void ClearMap ()
		{
			if (map == null)
				return;
			map.Clear ();
			map.MapType = GoogleMap.MapTypeNone;
		}

		public void OnMapReady (GoogleMap map)
		{
			this.map = map;
			RefreshMapLocation ();
		}

		public void RefreshMapLocation ()
		{
			if (map == null)
				return;
			var loc = Station.Location;
			var latlng = new LatLng (loc.Lat, loc.Lon);
			map.MoveCamera (CameraUpdateFactory.NewLatLngZoom (
				latlng, 17
			));
			map.Clear ();
			map.AddMarker (new MarkerOptions ().SetPosition (latlng));
			map.MapType = GoogleMap.MapTypeNormal;
		}

		public MapView MapView {
			get;
			private set;
		}

		public TextView StationName {
			get;
			private set;
		}

		public TextView SecondStationName {
			get;
			private set;
		}

		public TextView BikeNumber {
			get;
			private set;
		}

		public TextView SlotNumber {
			get;
			private set;
		}

		public Station Station {
			get;
			set;
		}
	}
}

