using System;

using Android.App;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using Android.Animation;

namespace Moyeu
{
	public class FlashBarController
	{
		FrameLayout layout;
		TextView text;
		ProgressBar progress;

		public FlashBarController (Activity parentActivity)
		{
			layout = parentActivity.FindViewById<FrameLayout> (Resource.Id.FlashBarLayout);
			text = parentActivity.FindViewById<TextView> (Resource.Id.FlashBarText);
			progress = parentActivity.FindViewById<ProgressBar> (Resource.Id.FlashBarProgress);
		}

		public void ShowLoading ()
		{
			text.Text = "Loading";
			progress.Visibility = ViewStates.Visible;
			layout.Visibility = ViewStates.Visible;
			layout.Animate ().Alpha (1).SetDuration (400).Start ();
		}

		public void ShowLoaded ()
		{
			text.Text = "Loaded";
			progress.Visibility = ViewStates.Gone;
			layout.Animate ().Alpha (0).SetStartDelay (500).SetDuration (400).WithEndAction (new Java.Lang.Runnable (() => {
				layout.Visibility = ViewStates.Gone;
			})).Start ();
		}

		public void ShowInformation (string info)
		{
			text.Text = info;
			progress.Visibility = ViewStates.Gone;
			layout.Visibility = ViewStates.Visible;
			layout.Animate ()
				.Alpha (1)
				.SetDuration (500)
				.WithEndAction (new Java.Lang.Runnable (() => layout.Animate ()
					                                        .Alpha (0)
					                                        .SetStartDelay (2000)
					                                        .SetDuration (400)
					                                        .Start ()))
				.Start ();
		}
	}
}

