
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
using Android.Support.Design.Widget;
using Android.Graphics.Drawables;
using Android.Content.Res;
using Android.Animation;
using Android.Views.Animations;

namespace Moyeu
{
	public class SwitchableFab : FloatingActionButton, ICheckable
	{
		AnimatorSet switchAnimation;
		bool state;
		Drawable srcFirst, srcSecond;
		ColorStateList backgroundTintFirst, backgroundTintSecond;

		static readonly int[] CheckedStateSet = {
			Android.Resource.Attribute.StateChecked
		};
		bool chked;

		public SwitchableFab (Context context) :
			base (context)
		{
			Initialize (context, null);
		}

		public SwitchableFab (Context context, IAttributeSet attrs) :
			base (context, attrs)
		{
			Initialize (context, attrs);
		}

		public SwitchableFab (Context context, IAttributeSet attrs, int defStyle) :
			base (context, attrs, defStyle)
		{
			Initialize (context, attrs);
		}

		public SwitchableFab (IntPtr handle, JniHandleOwnership own)
			: base (handle, own)
		{
		}

		void Initialize (Context context, IAttributeSet attrs)
		{
			srcFirst = this.Drawable;
			backgroundTintFirst = this.BackgroundTintList;

			if (attrs == null)
				return;
			var array = context.ObtainStyledAttributes (attrs, Resource.Styleable.SwitchableFab);
			srcSecond = array.GetDrawable (Resource.Styleable.SwitchableFab_srcSecond);
			backgroundTintSecond = array.GetColorStateList (Resource.Styleable.SwitchableFab_backgroundTintSecond);
			array.Recycle ();
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

		public void Switch ()
		{
			if (state)
				Switch (srcFirst, backgroundTintFirst);
			else
				Switch (srcSecond, backgroundTintSecond);
			state = !state;
		}

		void Switch (Drawable src, ColorStateList tint)
		{
			const int ScaleDuration = 200;
			const int AlphaDuration = 150;
			const int AlphaInDelay = 50;
			const int InitialDelay = 100;

			if (switchAnimation != null) {
				switchAnimation.Cancel ();
				switchAnimation = null;
			}

			var currentSrc = this.Drawable;

			// Scaling down animation
			var circleAnimOutX = ObjectAnimator.OfFloat (this, "scaleX", 1, 0.1f);
			var circleAnimOutY = ObjectAnimator.OfFloat (this, "scaleY", 1, 0.1f);
			circleAnimOutX.SetDuration (ScaleDuration);
			circleAnimOutY.SetDuration (ScaleDuration);

			// Alpha out of the icon
			var iconAnimOut = ObjectAnimator.OfInt (currentSrc, "alpha", 255, 0);
			iconAnimOut.SetDuration (AlphaDuration);

			var outSet = new AnimatorSet ();
			outSet.PlayTogether (circleAnimOutX, circleAnimOutY, iconAnimOut);
			outSet.SetInterpolator (AnimationUtils.LoadInterpolator (Context,
			                                                         Android.Resource.Animation.AccelerateInterpolator));
			outSet.StartDelay = InitialDelay;
			outSet.AnimationEnd += (sender, e) => {
				BackgroundTintList = tint;
				SetImageDrawable (src);
				JumpDrawablesToCurrentState ();
				((Animator)sender).RemoveAllListeners ();
			};

			// Scaling up animation
			var circleAnimInX = ObjectAnimator.OfFloat (this, "scaleX", 0.1f, 1);
			var circleAnimInY = ObjectAnimator.OfFloat (this, "scaleY", 0.1f, 1);
			circleAnimInX.SetDuration (ScaleDuration);
			circleAnimInY.SetDuration (ScaleDuration);

			// Alpha in of the icon
			src.Alpha = 0;
			var iconAnimIn = ObjectAnimator.OfInt (src, "alpha", 0, 255);
			iconAnimIn.SetDuration (AlphaDuration);
			iconAnimIn.StartDelay = AlphaInDelay;

			var inSet = new AnimatorSet ();
			inSet.PlayTogether (circleAnimInX, circleAnimInY, iconAnimIn);
			inSet.SetInterpolator (AnimationUtils.LoadInterpolator (Context,
			                                                        Android.Resource.Animation.DecelerateInterpolator));

			switchAnimation = new AnimatorSet ();
			switchAnimation.PlaySequentially (outSet, inSet);
			switchAnimation.Start ();
		}
	}
}

