
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

namespace Moyeu
{
	public class CheckedImageButton : ImageButton, ICheckable
	{
		static readonly int[] CheckedStateSet = {
			Android.Resource.Attribute.StateChecked
		};
		bool chked;

		public CheckedImageButton (Context context) :
			base (context)
		{
			Initialize ();
		}

		public CheckedImageButton (Context context, IAttributeSet attrs) :
			base (context, attrs)
		{
			Initialize ();
		}

		public CheckedImageButton (Context context, IAttributeSet attrs, int defStyle) :
			base (context, attrs, defStyle)
		{
			Initialize ();
		}

		public CheckedImageButton (IntPtr handle, JniHandleOwnership own)
			: base (handle, own)
		{
		}

		void Initialize ()
		{
		}

		public override bool PerformClick ()
		{
			Toggle ();
			return base.PerformClick ();
		}

		public void Toggle ()
		{
			JumpDrawablesToCurrentState ();
			Checked = !Checked;
		}

		public bool Checked {
			get {
				return chked;
			}
			set {
				if (chked == value)
					return;
				chked = value;
				RefreshDrawableState ();
			}
		}

		public override int[] OnCreateDrawableState (int extraSpace)
		{
			var space = extraSpace + (Checked ? CheckedStateSet.Length : 0);
			var drawableState = base.OnCreateDrawableState (space);
			if (Checked)
				MergeDrawableStates (drawableState, CheckedStateSet);
			return drawableState;
		}
	}
}

