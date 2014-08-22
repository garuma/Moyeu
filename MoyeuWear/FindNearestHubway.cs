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

using MoyeuWear;

namespace Moyeu
{
	public enum BikeActionStatus {
		Start,
		Stop
	}

	[Activity (Label = "Moyeu",
	           MainLauncher = true,
	           Icon = "@drawable/icon",
	           Theme = "@android:style/Theme.DeviceDefault.Light")]
	[IntentFilter (new[] { "vnd.google.fitness.TRACK" },
	               DataMimeType = "vnd.google.fitness.activity/biking",
	               Categories = new string[] { "android.intent.category.DEFAULT" })]
	public class MainActivity : InsetActivity,
	IMessageApiMessageListener, IGoogleApiClientConnectionCallbacks, IGoogleApiClientOnConnectionFailedListener, IResultCallback
	{
		IGoogleApiClient client;
		GridViewPager pager;
		TextView label;

		Handler handler;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			handler = new Handler ();
			client = new GoogleApiClientBuilder (this, this, this)
				.AddApi (WearableClass.Api)
				.Build ();
		}

		BikeActionStatus ActionStatus {
			get {
				return Intent.GetStringExtra ("actionStatus") == "CompletedActionStatus" ?
					BikeActionStatus.Stop : BikeActionStatus.Start;
			}
		}

		public override void OnReadyForContent ()
		{
			SetContentView (Resource.Layout.Main);
			pager = FindViewById<GridViewPager> (Resource.Id.pager);
			label = FindViewById<TextView> (Resource.Id.loading);
		}

		protected override void OnStart ()
		{
			base.OnStart ();
			client.Connect ();
		}

		public void OnMessageReceived (IMessageEvent message)
		{
			if (!message.Path.EndsWith ("/Answer"))
				return;

			IList<Station> stations = null;
			GeoPoint currentLocation;
			using (var content = new System.IO.MemoryStream (message.GetData ())) {
				currentLocation = GeoUtils.ParseFromStream (content);
				stations = StationUtils.ParseStations (content);
			}

			if (stations != null) {
				handler.Post (() => {
					var adapter = new StationGridAdapter (FragmentManager, stations, currentLocation, ActionStatus);
					pager.Adapter = adapter;
					pager.OffscreenPageCount = 2;
					label.Visibility = ViewStates.Gone;
					pager.Visibility = ViewStates.Visible;
				});
			}
		}

		public void OnConnected (Bundle p0)
		{
			WearableClass.MessageApi.AddListener (client, this);
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
			WearableClass.MessageApi.RemoveListener (client, this);
		}

		public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
		{
			Android.Util.Log.Error ("GMS", "Connection failed " + result.ErrorCode);
		}

		/*void Android.Gms.Common.IGooglePlayServicesClientOnConnectionFailedListener.OnConnectionFailed (Android.Gms.Common.ConnectionResult p0)
		{
		}*/

		void GetStations ()
		{
			WearableClass.NodeApi.GetConnectedNodes (client)
				.SetResultCallback (this);
		}

		int state = 0;

		public void OnResult (Java.Lang.Object result)
		{
			if (state == 0) {
				state = 1;
				var apiResult = result.JavaCast<INodeApiGetConnectedNodesResult> ();
				var nodes = apiResult.Nodes;
				var phoneNode = nodes.FirstOrDefault ();
				if (phoneNode == null) {
					DisplayError ();
					return;
				}

				WearableClass.MessageApi.SendMessage (client, phoneNode.Id,
				                                      "/moyeu/SearchNearestStations/" + ActionStatus.ToString (),
				                                      new byte[0]).SetResultCallback (this);
			} else {
				state = 0;
				var apiResult = result.JavaCast<IMessageApiSendMessageResult> ();
				Android.Util.Log.Info ("SendMessage", apiResult.RequestId.ToString ());
			}
		}
	}
}


