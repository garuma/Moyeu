
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
		int buttonDrawableId;

		string text;
		Action callback;

		Tuple<string, string> texts;
		bool initialState;
		Action<bool> toggleCallback;

		public static ActionButtonFragment WithAction (string text, int buttonDrawableId, Action callback = null)
		{
			var r = new ActionButtonFragment ();
			r.text = text;
			r.buttonDrawableId = buttonDrawableId;
			r.callback = callback;
			return r;
		}

		public static ActionButtonFragment WithToggleAction (Tuple<string, string> texts, int buttonDrawableId, bool initialState, Action<bool> callback = null)
		{
			var r = new ActionButtonFragment ();
			r.texts = texts;
			r.buttonDrawableId = buttonDrawableId;
			r.toggleCallback = callback;
			r.initialState = initialState;
			return r;
		}

		public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
		{
			return inflater.Inflate (Resource.Layout.ActionButtonLayout, container, false);
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			base.OnViewCreated (view, savedInstanceState);
			var label = view.FindViewById<TextView> (Resource.Id.actionText);
			var icon = view.FindViewById<ImageView> (Resource.Id.actionButton);
			var toggle = view.FindViewById<ToggleButton> (Resource.Id.toggleActionButton);

			if (texts == null) {
				toggle.Visibility = ViewStates.Gone;
				icon.SetImageResource (buttonDrawableId);
				if (callback != null)
					icon.Click += (sender, e) => callback ();
				label.Text = text;
			} else {
				icon.Visibility = ViewStates.Gone;
				toggle.SetButtonDrawable (buttonDrawableId);
				label.Text = initialState ? texts.Item2 : texts.Item1;
				toggle.Checked = initialState;
				if (toggleCallback != null) {
					toggle.CheckedChange += (sender, e) => {
						toggle.Text = e.IsChecked ? texts.Item2 : texts.Item1;
						toggleCallback (e.IsChecked);
					};
				}
			}
		}
	}
}

