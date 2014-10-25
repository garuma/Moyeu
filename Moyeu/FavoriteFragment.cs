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

using Rdio.TangoAndCache.Android.UI.Drawables;

namespace Moyeu
{
	public class FavoriteFragment : Android.Support.V4.App.ListFragment, IMoyeuSection
	{
		FavoriteManager favManager;
		FavoriteAdapter adapter;
		Action<long> stationShower;

		public FavoriteFragment (Context context, Action<long> stationShower)
		{
			this.stationShower = stationShower;
			this.favManager = FavoriteManager.Obtain (context);
			ListAdapter = this.adapter = new FavoriteAdapter (context);
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
			var favs = favManager.LastFavorites
			           ?? (await favManager.GetFavoriteStationIdsAsync ());
			adapter.SetStationIds (favs);
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);
			view.SetBackgroundDrawable (AndroidExtensions.DefaultBackground);
			SetEmptyText ("You have no favorites yet");
			ListView.ItemClick += (sender, e) => stationShower (e.Id);
		}
	}

	class FavoriteAdapter : BaseAdapter
	{
		List<Station> stations;
		Context context;
		XamSvg.PictureBitmapDrawable bikePicture;
		XamSvg.PictureBitmapDrawable mapPlaceholder;

		public FavoriteAdapter (Context context)
		{
			this.context = context;
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
			var view = EnsureView (convertView);
			var version = Interlocked.Increment (ref view.VersionNumber);

			var mapView = view.FindViewById<ImageView> (Resource.Id.StationMap);
			var stationName = view.FindViewById<TextView> (Resource.Id.MainStationName);
			var secondStationName = view.FindViewById<TextView> (Resource.Id.SecondStationName);
			var bikeNumber = view.FindViewById<TextView> (Resource.Id.BikeNumber);
			var slotNumber = view.FindViewById<TextView> (Resource.Id.SlotNumber);
			var bikeImage = view.FindViewById<ImageView> (Resource.Id.bikeImageView);
			if (bikePicture == null)
				bikePicture = XamSvg.SvgFactory.GetDrawable (context.Resources, Resource.Raw.bike);
			bikeImage.SetImageDrawable (bikePicture);
			if (mapPlaceholder == null)
				mapPlaceholder = XamSvg.SvgFactory.GetDrawable (context.Resources, Resource.Raw.map_placeholder);
			mapView.SetImageDrawable (mapPlaceholder);

			var station = stations [position];
			view.Station = station;

			string secondPart;
			stationName.Text = StationUtils.CutStationName (station.Name, out secondPart);
			secondStationName.Text = secondPart;
			bikeNumber.Text = station.BikeCount.ToString ();
			slotNumber.Text = station.Capacity.ToString ();

			var api = GoogleApis.Obtain (context);
			string mapUrl = GoogleApis.MakeMapUrl (station.Location);
			SelfDisposingBitmapDrawable mapDrawable;
			if (api.MapCache.TryGet (mapUrl, out mapDrawable))
				mapView.SetImageDrawable (mapDrawable);
			else
				api.LoadMap (station.Location, view, mapView, version);

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

