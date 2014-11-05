using System;
using System.Collections.Generic;
using Android.Views;
using Android.Animation;

using Android.Support.V4.View;
using Runnable = Java.Lang.Runnable;
using Android.Views.Animations;
using Android.Graphics;

namespace Moyeu
{
	public static class AnimationExtensions
	{
		public static void AlphaAnimate (this View view, float alpha, int duration = 300, Action endAction = null, int startDelay = 0)
		{
			var animator = ViewCompat.Animate (view);
			animator
				.SetDuration (duration)
				.SetStartDelay (startDelay)
				.Alpha (alpha);
			if (endAction != null)
				animator.WithEndAction (new Runnable (endAction));
			animator.Start ();
		}

		public static void TranslationYAnimate (this View view, int translation, int duration = 300,
		                                        IInterpolator interpolator = null, Action endAction = null)
		{
			var animator = ViewCompat.Animate (view);
			animator
				.SetDuration (duration)
				.TranslationY (translation);
			if (endAction != null)
				animator.WithEndAction (new Runnable (endAction));
			if (interpolator != null)
				animator.SetInterpolator (interpolator);
			animator.Start ();
		}
	}
}

