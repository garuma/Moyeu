
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Graphics.Drawables;

using Android.Support.Wearable.Views;

using MoyeuWear;

namespace Moyeu
{
	public class ActionButtonFragment : Fragment
	{
		string text;
		int buttonDrawableId;

		public static ActionButtonFragment WithAction (string text, int buttonDrawableId)
		{
			var r = new ActionButtonFragment ();
			r.text = text;
			r.buttonDrawableId = buttonDrawableId;
			return r;
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			return inflater.Inflate (Resource.Layout.ActionButtonLayout, container, false);
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);
			view.FindViewById<TextView> (Resource.Id.actionText).Text = text;
			view.FindViewById<ImageView> (Resource.Id.actionButton).SetImageResource (buttonDrawableId);
		}
	}
}

