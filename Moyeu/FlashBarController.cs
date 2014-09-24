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

		public FlashBarController (View parentView)
		{
			layout = parentView.FindViewById<FrameLayout> (Resource.Id.FlashBarLayout);
			text = parentView.FindViewById<TextView> (Resource.Id.FlashBarText);
		}

		public void ShowLoading ()
		{
			text.Text = "Loading";
			layout.Visibility = ViewStates.Visible;
			layout.AlphaAnimate (1, duration: 400);
		}

		public void ShowLoaded ()
		{
			layout.AlphaAnimate (0, duration: 400, endAction: () => {
				layout.Visibility = ViewStates.Gone;
			});
		}

		public void ShowInformation (string info)
		{
			text.Text = info;
			layout.Visibility = ViewStates.Visible;
			layout.AlphaAnimate (1, duration: 500, endAction: () => {
				layout.AlphaAnimate (0, duration: 400, startDelay: 2000);
			});
		}
	}
}

