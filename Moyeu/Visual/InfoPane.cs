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
using Android.Animation;
using Android.Graphics;
using Android.Graphics.Drawables;
using Runnable = Java.Lang.Runnable;
using Android.Views.Animations;

namespace Moyeu
{
	public class InfoPane : LinearLayout
	{
		public enum State {
			Closed,
			Opened,
			FullyOpened
		}

		public event Action<State> StateChanged;

		State state;
		int contentOffsetY;
		bool isAnimating;
		IInterpolator smoothInterpolator = new SmoothInterpolator ();
		VelocityTracker velocityTracker;
		GestureDetector paneGestureDetector;
		State stateBeforeTracking;
		bool isTracking;
		bool preTracking;
		int startY = -1;
		float oldY;

		int pagingTouchSlop;
		int minFlingVelocity;
		int maxFlingVelocity;

		GradientDrawable shadowDrawable;

		public InfoPane (Context context) : base (context)
		{
			Initialize ();
		}

		public InfoPane (Context context, IAttributeSet attrs) : base (context, attrs)
		{
			Initialize ();
		}

		public InfoPane (Context context, IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
		{
			Initialize ();
		}

		void Initialize ()
		{
			var config = ViewConfiguration.Get (Context);
			this.pagingTouchSlop = config.ScaledPagingTouchSlop;
			this.minFlingVelocity = config.ScaledMinimumFlingVelocity;
			this.maxFlingVelocity = config.ScaledMaximumFlingVelocity;
			const int BaseShadowColor = 0;
			var shadowColors = new[] {
				Color.Argb (0x30, BaseShadowColor, BaseShadowColor, BaseShadowColor).ToArgb (),
				Color.Argb (0, BaseShadowColor, BaseShadowColor, BaseShadowColor).ToArgb ()
			};
			this.shadowDrawable = new GradientDrawable (GradientDrawable.Orientation.BottomTop,
			                                            shadowColors);
			var elevation = Resources.GetDimension (Resource.Dimension.design_fab_elevation);
			Android.Support.V4.View.ViewCompat.SetElevation (this, elevation);
		}

		public bool Opened {
			get {
				return state == State.Opened || state == State.FullyOpened;
			}
		}

		public bool FullyOpened {
			get {
				return state == State.FullyOpened;
			}
		}

		public void SetState (State newState, bool animated = true)
		{
			var interpolator = smoothInterpolator;
			this.state = newState;
			if (newState != State.Closed)
				Visibility = ViewStates.Visible;
			if (!animated) {
				SetNewOffset (OffsetForState (newState));
				if (state == State.Closed)
					Visibility = ViewStates.Invisible;
			} else {
				isAnimating = true;
				var duration = Context.Resources.GetInteger (Android.Resource.Integer.ConfigMediumAnimTime);
				this.TranslationYAnimate (OffsetForState (newState), duration, interpolator, () => {
					isAnimating = false;
					if (state == State.Closed)
						Visibility = ViewStates.Invisible;
				});
			}
			if (StateChanged != null)
				StateChanged (newState);
		}

		int OffsetForState (State state)
		{
			switch (state) {
			default:
			case State.Closed:
				return Height;
			case State.FullyOpened:
				return 0;
			case State.Opened:
				return Height - FindViewById (Resource.Id.PaneHeaderView).Height - PaddingTop;
			}
		}

		void SetNewOffset (int newOffset)
		{
			if (state == State.Closed) {
				TranslationY = contentOffsetY = newOffset;
			} else {
				contentOffsetY = Math.Min (Math.Max (OffsetForState (State.FullyOpened), newOffset),
				                           OffsetForState (State.Opened));
				TranslationY = contentOffsetY;
			}
		}

		/*public override bool OnInterceptTouchEvent (MotionEvent ev)
		{
			// Only accept single touch
			if (ev.PointerCount != 1)
				return false;
			ev.OffsetLocation (0, TranslationY);
			return CaptureMovementCheck (ev);
		}*/

		public override bool OnTouchEvent (MotionEvent e)
		{
			if (paneGestureDetector == null) {
				var l = new DoubleTapListener (() => SetState (Opened && FullyOpened ? State.Opened : State.FullyOpened));
				paneGestureDetector = new GestureDetector (Context, l);
			}
			paneGestureDetector.OnTouchEvent (e);

			e.OffsetLocation (0, TranslationY);
			if (e.Action == MotionEventActions.Down) {
				CaptureMovementCheck (e);
				return true;
			}
			if (!isTracking && !CaptureMovementCheck (e))
				return true;

			if (e.Action != MotionEventActions.Move || MoveDirectionTest (e))
				velocityTracker.AddMovement (e);

			if (e.Action == MotionEventActions.Move) {
				var y = e.GetY ();
				// We don't want to go beyond startY
				if (state == State.Opened && y > startY
				    || state == State.FullyOpened && y < startY)
					return true;
				// We reset the velocity tracker in case a movement goes back to its origin
				if (state == State.Opened && y > oldY
				    || state == State.FullyOpened && y < oldY)
					velocityTracker.Clear ();

				var traveledDistance = (int)Math.Round (Math.Abs (y - startY));
				if (state == State.Opened)
					traveledDistance = OffsetForState (State.Opened) - traveledDistance;
				SetNewOffset (traveledDistance);
				oldY = y;
			} else if (e.Action == MotionEventActions.Up) {
				velocityTracker.ComputeCurrentVelocity (1000, maxFlingVelocity);
				if (Math.Abs (velocityTracker.YVelocity) > minFlingVelocity
				    && Math.Abs (velocityTracker.YVelocity) < maxFlingVelocity)
					SetState (state == State.FullyOpened ? State.Opened : State.FullyOpened);
				else if (state == State.FullyOpened && contentOffsetY > Height / 2)
					SetState (State.Opened);
				else if (state == State.Opened && contentOffsetY < Height / 2)
					SetState (State.FullyOpened);
				else
					SetState (state);

				preTracking = isTracking = false;
				velocityTracker.Clear ();
				velocityTracker.Recycle ();
			}

			return true;
		}

		bool CaptureMovementCheck (MotionEvent ev)
		{
			if (ev.Action == MotionEventActions.Down) {
				oldY = startY = (int)ev.GetY ();

				if (!Opened)
					return false;

				velocityTracker = VelocityTracker.Obtain ();
				velocityTracker.AddMovement (ev);
				preTracking = true;
				stateBeforeTracking = state;
				return false;
			}

			if (ev.Action == MotionEventActions.Up)
				preTracking = isTracking = false;

			if (!preTracking)
				return false;

			velocityTracker.AddMovement (ev);

			if (ev.Action == MotionEventActions.Move) {

				// Check we are going in the right direction, if not cancel the current gesture
				if (!MoveDirectionTest (ev)) {
					preTracking = false;
					return false;
				}

				// If the current gesture has not gone long enough don't intercept it just yet
				var distance = Math.Abs (ev.GetY () - startY);
				if (distance < pagingTouchSlop)
					return false;
			}

			oldY = startY = (int)ev.GetY ();
			isTracking = true;

			return true;
		}

		// Check that movement is going in the right direction
		bool MoveDirectionTest (MotionEvent e)
		{
			return (stateBeforeTracking == State.FullyOpened ? e.GetY () >= startY : e.GetY () <= startY);
		}

		protected override void DispatchDraw (Android.Graphics.Canvas canvas)
		{
			base.DispatchDraw (canvas);

			if (shadowDrawable != null && (state == State.Opened || isTracking || isAnimating)) {
				// Draw inset shadow on top of the pane
				shadowDrawable.SetBounds (0, 0, Width, PaddingTop);
				shadowDrawable.Draw (canvas);
			}
		}

		class SmoothInterpolator : Java.Lang.Object, IInterpolator, ITimeInterpolator
		{
			public float GetInterpolation (float input)
			{
				return (float)Math.Pow (input - 1, 5) + 1;
			}
		}

		class DoubleTapListener : GestureDetector.SimpleOnGestureListener
		{
			Action callback;

			public DoubleTapListener (Action callback)
			{
				this.callback = callback;
			}

			public override bool OnDoubleTap (MotionEvent e)
			{
				callback ();
				return true;
			}
		}
	}
}

