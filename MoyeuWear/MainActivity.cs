using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using Android.Support.Wearable;
using Android.Support.Wearable.Activity;
using Android.Support.Wearable.Views;

using Android.Gms.Common.Apis;
using Android.Gms.Wearable;
using ConnectionCallbacks = Android.Gms.Common.Apis.GoogleApiClient.IConnectionCallbacks;
using ConnectionFailedListener = Android.Gms.Common.Apis.GoogleApiClient.IOnConnectionFailedListener;

using MoyeuWear;
using Android.Graphics;

namespace Moyeu
{
	public enum BikeActionStatus {
		Start,
		Stop
	}

	[Activity (Label = "Moyeu",
	           MainLauncher = true,
	           Icon = "@drawable/icon",
	           Theme = "@android:style/Theme.DeviceDefault.Light",
	           NoHistory = true,
	           StateNotNeeded = true)]
	[IntentFilter (new[] { "vnd.google.fitness.TRACK" },
	               DataMimeType = "vnd.google.fitness.activity/biking",
	               Categories = new string[] { "android.intent.category.DEFAULT" })]
	public class MainActivity : Activity,
	IDataApiDataListener, ConnectionCallbacks, ConnectionFailedListener, IResultCallback, IMoyeuActions
	{
		const string SearchStationPath = "/moyeu/SearchNearestStations";
		const string StationBackgroundsPath = "/moyeu/StationBackgrounds";
		const string BackgroundImageKey = "background-";

		GoogleApiClient client;
		INode phoneNode;

		GridViewPager pager;
		ProgressBar loading;
		Switch countSwitch;
		DotsPageIndicator dotsIndicator;

		StationGridAdapter adapter;
		Handler handler;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			handler = new Handler ();
			client = new GoogleApiClient.Builder (this, this, this)
				.AddApi (WearableClass.API)
				.Build ();

			SetContentView (Resource.Layout.Main);
			pager = FindViewById<GridViewPager> (Resource.Id.pager);
			loading = FindViewById<ProgressBar> (Resource.Id.loading);
			countSwitch = FindViewById<Switch> (Resource.Id.metricSwitch);
			dotsIndicator = FindViewById<DotsPageIndicator> (Resource.Id.dotsIndicator);

			countSwitch.Checked = ActionStatus == BikeActionStatus.Stop;
			countSwitch.CheckedChange += HandleCheckedChange;
			dotsIndicator.PageScrolled += HandlePageScrolled;
			dotsIndicator.SetPager (pager);
		}

		BikeActionStatus ActionStatus {
			get {
				return Intent.GetStringExtra ("actionStatus") == "CompletedActionStatus" ?
					BikeActionStatus.Stop : BikeActionStatus.Start;
			}
		}

		void HandleCheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
		{
			if (adapter == null)
				return;
			adapter.SwitchCount ();
		}

		void HandlePageScrolled (object sender, GridViewPager.PageScrolledEventArgs e)
		{
			var col = e.Column;

			if (col == 0) {
				var offset = e.ColOffset;
				var w = pager.Width;

				countSwitch.TranslationX = -(w * offset);

				if (adapter != null) {
					var bg = (ShadowedBitmapDrawable)adapter.GetBackgroundForRow (e.RowValue);
					bg.SetShadowLevel (offset);
				}
			}
		}

		protected override void OnStart ()
		{
			base.OnStart ();
			client.Connect ();
		}

		public void OnDataChanged (DataEventBuffer dataEvents)
		{
			var dataEvent = Enumerable.Range (0, dataEvents.Count)
				.Select (i => dataEvents.Get (i).JavaCast<IDataEvent> ())
				.FirstOrDefault (de => de.Type == DataEvent.TypeChanged && de.DataItem.Uri.Path == SearchStationPath + "/Answer");
			if (dataEvent == null)
				return;
			var dataMapItem = DataMapItem.FromDataItem (dataEvent.DataItem);
			var map = dataMapItem.DataMap;
			var data = map.GetDataMapArrayList ("Stations");

			ProcessRawStationData (data);
		}

		async void ProcessRawStationData (IList<DataMap> data)
		{
			var stations = new List<SimpleStation> ();
			foreach (var d in data) {
				stations.Add (new SimpleStation {
					Id = d.GetInt ("Id", 0),
					Primary = d.GetString ("Primary", "<no name>"),
					Secondary = d.GetString ("Secondary", "<no name>"),
					Background = await GetBitmapForAsset (d.GetAsset ("Background")),
					Bikes = d.GetInt ("Bikes", 0),
					Racks = d.GetInt ("Racks", 0),
					Distance = d.GetDouble ("Distance", 0),
					Lat = d.GetDouble ("Lat", 0),
					Lon = d.GetDouble ("Lon", 0),
					IsFavorite = d.GetBoolean ("IsFavorite", false),
				});
			}

			if (stations.Any ()) {
				adapter = new StationGridAdapter (FragmentManager,
				                                  stations,
				                                  this);
				pager.Adapter = adapter;
				pager.OffscreenPageCount = 5;
				loading.Visibility = ViewStates.Invisible;
				pager.Visibility = ViewStates.Visible;
				countSwitch.Visibility = ViewStates.Visible;
				dotsIndicator.Visibility = ViewStates.Visible;
			}
		}

		async Task<Bitmap> GetBitmapForAsset (Asset asset)
		{
			var result = await WearableClass.DataApi.GetFdForAsset (client, asset)
											.AsAsync<IDataApiGetFdForAssetResult> ()
			                                .ConfigureAwait (false);
			var stream = result.InputStream;
			return BitmapFactory.DecodeStream (stream);
		}

		public void OnConnected (Bundle p0)
		{
			WearableClass.DataApi.AddListener (client, this);
			GetStations ();
		}

		void DisplayError ()
		{
			Finish ();
			var intent = new Intent (this, typeof(ConfirmationActivity));
			intent.PutExtra (ConfirmationActivity.ExtraAnimationType, ConfirmationActivity.FailureAnimation);
			intent.PutExtra (ConfirmationActivity.ExtraMessage, "Can't find phone");
			StartActivity (intent);
		}

		protected override void OnStop ()
		{
			base.OnStop ();
			client.Disconnect ();
		}

		public void OnConnectionSuspended (int reason)
		{
			Android.Util.Log.Error ("GMS", "Connection suspended " + reason);
			WearableClass.DataApi.RemoveListener (client, this);
		}

		public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
			Android.Util.Log.Error ("GMS", "Connection failed " + result.ErrorCode);
		}

		void GetStations ()
		{
			WearableClass.CapabilityApi.GetCapability (client,
			                                           "hubway_station_fetcher",
			                                           CapabilityApi.FilterReachable)
				.SetResultCallback (this);
		}

		public void OnResult (Java.Lang.Object result)
		{
			var apiResult = result.JavaCast<ICapabilityApiGetCapabilityResult> ();
			var nodes = apiResult.Capability.Nodes;
			phoneNode = nodes.FirstOrDefault ();
			if (phoneNode == null) {
				DisplayError ();
				return;
			}

			WearableClass.MessageApi.SendMessage (client, phoneNode.Id,
			                                      SearchStationPath + ActionStatus.ToString (),
			                                      new byte[0]);
		}

		public void NavigateToStation (SimpleStation station)
		{
			Finish ();
			var culture = System.Globalization.CultureInfo.InvariantCulture;
			var req = string.Format ("google.navigation:///?q={0},{1}&mode=w",
			                         station.Lat.ToString (culture),
			                         station.Lon.ToString (culture));
			var geoUri = Android.Net.Uri.Parse (req);
			var intent = new Intent (Intent.ActionView, geoUri);
			//intent.SetPackage ("com.google.android.apps.maps");
			StartActivity (intent);
		}

		public void ToggleFavoriteStation (int stationId, bool cked)
		{
			var path = "/moyeu/Action/Favorite/" + stationId + "?" + (cked ? "add" : "remove");
			SendMessage (path);
		}

		public BikeActionStatus CurrentStatus {
			get {
				return countSwitch == null ? ActionStatus :
					countSwitch.Checked ? BikeActionStatus.Stop : BikeActionStatus.Start;
			}
		}

		void SendMessage (string path)
		{
			WearableClass.MessageApi.SendMessage (client, phoneNode.Id, path, new byte[0]);
		}
	}
}


