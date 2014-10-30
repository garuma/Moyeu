using System;

using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Content;
using Android.Views;

using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Widget;
using Android.Content.Res;
using Android.Util;

namespace Moyeu
{
	public class LollipopDrawerToggle : Java.Lang.Object, DrawerLayout.IDrawerListener
	{
		readonly Activity mActivity;
		readonly DrawerLayout mDrawerLayout;
		DrawerArrowDrawable mSlider;
		Drawable mHomeAsUpIndicator;
		bool mDrawerIndicatorEnabled = true;
		bool mHasCustomUpIndicator;
		readonly int mOpenDrawerContentDescRes;
		readonly int mCloseDrawerContentDescRes;

		public LollipopDrawerToggle(Activity activity, DrawerLayout drawerLayout, int openDrawerContentDescRes, int closeDrawerContentDescRes)
		{
			this.mActivity = activity;
			this.mDrawerLayout = drawerLayout;
			this.mOpenDrawerContentDescRes = openDrawerContentDescRes;
			this.mCloseDrawerContentDescRes = closeDrawerContentDescRes;
			this.mSlider = new DrawerArrowDrawable (activity, ActionBarThemedContext);

			this.mHomeAsUpIndicator = GetThemeUpIndicator();
		}

		public void SyncState ()
		{
			mSlider.setPosition(mDrawerLayout.IsDrawerOpen ((int)GravityFlags.Start) ? 1f : 0f);
			if (this.mDrawerIndicatorEnabled)
				SetActionBarUpIndicator((Drawable)this.mSlider, this.mDrawerLayout.IsDrawerOpen((int)GravityFlags.Start)
				                        ? this.mCloseDrawerContentDescRes : this.mOpenDrawerContentDescRes);
		}

		public void OnConfigurationChanged(Configuration newConfig)
		{
			if (!this.mHasCustomUpIndicator)
				this.mHomeAsUpIndicator = GetThemeUpIndicator();
			SyncState();
		}

		public bool OnOptionsItemSelected(IMenuItem item)
		{
			if ((item != null) && (item.ItemId == Android.Resource.Id.Home) && (this.mDrawerIndicatorEnabled)) {
				Toggle();
				return true;
			}
			return false;
		}

		void Toggle() {
			if (this.mDrawerLayout.IsDrawerVisible((int)GravityFlags.Start))
			    this.mDrawerLayout.CloseDrawer((int)GravityFlags.Start);
			else
			    this.mDrawerLayout.OpenDrawer((int)GravityFlags.Start);
		}

		public void SetHomeAsUpIndicator(Drawable indicator)
		{
			if (indicator == null) {
				this.mHomeAsUpIndicator = GetThemeUpIndicator();
				this.mHasCustomUpIndicator = false;
			} else {
				this.mHomeAsUpIndicator = indicator;
				this.mHasCustomUpIndicator = true;
			}

			if (!this.mDrawerIndicatorEnabled)
				SetActionBarUpIndicator(this.mHomeAsUpIndicator, 0);
		}

		public void SetHomeAsUpIndicator(int resId)
		{
			Drawable indicator = null;
			if (resId != 0) {
				indicator = this.mDrawerLayout.Resources.GetDrawable(resId);
			}
			SetHomeAsUpIndicator(indicator);
		}

		public bool IsDrawerIndicatorEnabled()
		{
			return this.mDrawerIndicatorEnabled;
		}

		public void SetDrawerIndicatorEnabled(bool enable)
		{
			if (enable != this.mDrawerIndicatorEnabled) {
				if (enable) {
					SetActionBarUpIndicator((Drawable)this.mSlider, this.mDrawerLayout.IsDrawerOpen((int)GravityFlags.Start)
					                        ? this.mCloseDrawerContentDescRes : this.mOpenDrawerContentDescRes);
				}
				else
				{
					SetActionBarUpIndicator(this.mHomeAsUpIndicator, 0);
				}
				this.mDrawerIndicatorEnabled = enable;
			}
		}

		public void OnDrawerSlide(View drawerView, float slideOffset)
		{
			this.mSlider.setPosition(Math.Min(1.0F, Math.Max(0.0F, slideOffset)));
		}

		public void OnDrawerOpened(View drawerView)
		{
			this.mSlider.setPosition(1.0F);
			if (this.mDrawerIndicatorEnabled)
				SetActionBarDescription(this.mCloseDrawerContentDescRes);
		}

		public void OnDrawerClosed(View drawerView)
		{
			this.mSlider.setPosition(0.0F);
			if (this.mDrawerIndicatorEnabled)
				SetActionBarDescription(this.mOpenDrawerContentDescRes);
		}

		public void OnDrawerStateChanged(int newState)
		{
		}

		void SetActionBarUpIndicator(Drawable drawable, int contentDescRes) {
			ActionBar actionBar = mActivity.ActionBar;
			if (actionBar != null) {
				actionBar.SetHomeAsUpIndicator (drawable);
				actionBar.SetHomeActionContentDescription (contentDescRes);
			}
		}

		void SetActionBarDescription(int contentDescRes) {
			ActionBar actionBar = mActivity.ActionBar;
			if (actionBar != null)
				actionBar.SetHomeActionContentDescription (contentDescRes);
		}

		Drawable GetThemeUpIndicator() {
			TypedArray a = ActionBarThemedContext.ObtainStyledAttributes (new int[] {
				Android.Resource.Attribute.HomeAsUpIndicator
			});

			Drawable result = a.GetDrawable(0);
			a.Recycle();
			return result;
		}

		Context ActionBarThemedContext {
			get {
				ActionBar actionBar = this.mActivity.ActionBar;
				return actionBar != null ? actionBar.ThemedContext : mActivity;
			}
		}
	}

	class DrawerArrowDrawable: Drawable
	{
		private readonly Paint mPaint = new Paint();
		readonly Activity mActivity;
		private static readonly float ARROW_HEAD_ANGLE = (float)((45.0 * Math.PI) / 180);
		private readonly float mBarThickness;
		private readonly float mTopBottomArrowSize;
		private readonly float mBarSize;
		private readonly float mMiddleArrowSize;
		private readonly float mBarGap;
		private readonly bool mSpin;
		private readonly Path mPath = new Path();
		private readonly int mSize;
		private bool mVerticalMirror = false;
		private float mProgress;

		public DrawerArrowDrawable (Activity activity, Context themedContext)
		{
			this.mPaint.AntiAlias = true;

			this.mActivity = activity;
			var context = themedContext;
			var metrics = context.Resources.DisplayMetrics;

			this.mPaint.Color = context.Resources.GetColor (Resource.Color.white_primary);
			this.mSize = (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, 24, metrics);
			this.mBarSize = TypedValue.ApplyDimension (ComplexUnitType.Dip, 18, metrics);
			this.mTopBottomArrowSize = TypedValue.ApplyDimension (ComplexUnitType.Dip, 11.31f, metrics);

			this.mBarThickness = TypedValue.ApplyDimension (ComplexUnitType.Dip, 2, metrics);
			this.mBarGap = TypedValue.ApplyDimension (ComplexUnitType.Dip, 3, metrics);
			this.mSpin = true;
			this.mMiddleArrowSize = TypedValue.ApplyDimension (ComplexUnitType.Dip, 16, metrics);

			this.mPaint.SetStyle (Paint.Style.Stroke);
			this.mPaint.StrokeJoin = Paint.Join.Round;
			this.mPaint.StrokeCap = Paint.Cap.Square;
			this.mPaint.StrokeWidth = mBarThickness;
		}

		public void setPosition(float position) {
			if (position == 1.0F)
				setVerticalMirror(true);
			else if (position == 0.0F) {
				setVerticalMirror(false);
			}
			setProgress(position);
		}

		bool isLayoutRtl()
		{
			return ViewCompat.GetLayoutDirection (this.mActivity.Window.DecorView) == 1;
		}

		public float getPosition()
		{
			return getProgress();
		}

		protected void setVerticalMirror(bool verticalMirror)
		{
			this.mVerticalMirror = verticalMirror;
		}

		public override void Draw(Canvas canvas)
		{
			Rect bounds = Bounds;
			bool isRtl = isLayoutRtl();

			float arrowSize = lerp(this.mBarSize, this.mTopBottomArrowSize, this.mProgress);
			float middleBarSize = lerp(this.mBarSize, this.mMiddleArrowSize, this.mProgress);

			float middleBarCut = lerp(0.0F, this.mBarThickness / 2.0F, this.mProgress);

			float rotation = lerp(0.0F, ARROW_HEAD_ANGLE, this.mProgress);

			float canvasRotate = lerp(isRtl ? 0.0F : -180.0F, isRtl ? 180.0F : 0.0F, this.mProgress);
			float topBottomBarOffset = lerp(this.mBarGap + this.mBarThickness, 0.0F, this.mProgress);
			this.mPath.Rewind();

			float arrowEdge = -middleBarSize / 2.0F;

			this.mPath.MoveTo(arrowEdge + middleBarCut, 0.0F);
			this.mPath.RLineTo(middleBarSize - middleBarCut, 0.0F);

			float arrowWidth = (float)Math.Round(arrowSize * Math.Cos(rotation));
			float arrowHeight = (float)Math.Round(arrowSize * Math.Sin(rotation));

			this.mPath.MoveTo(arrowEdge, topBottomBarOffset);
			this.mPath.RLineTo(arrowWidth, arrowHeight);

			this.mPath.MoveTo(arrowEdge, -topBottomBarOffset);
			this.mPath.RLineTo(arrowWidth, -arrowHeight);
			this.mPath.MoveTo(0.0F, 0.0F);
			this.mPath.Close();

			canvas.Save();

			if (this.mSpin) {
				canvas.Rotate(canvasRotate * ((this.mVerticalMirror ^ isRtl) ? -1 : 1), bounds.CenterX(), bounds.CenterY());
			}
			else if (isRtl) {
				canvas.Rotate(180.0F, bounds.CenterX(), bounds.CenterY());
			}
			canvas.Translate(bounds.CenterX(), bounds.CenterY());
			canvas.DrawPath(this.mPath, this.mPaint);

			canvas.Restore();
		}

		public override void SetAlpha (int alpha)
		{
			this.mPaint.Alpha = alpha;
		}

		public override bool AutoMirrored {
			get {
				return true;
			}
			set {}
		}

		public override void SetColorFilter(ColorFilter colorFilter)
		{
			this.mPaint.SetColorFilter (colorFilter);
		}


		public override int IntrinsicHeight {
			get {
				return mSize;
			}
		}

		public override int IntrinsicWidth {
			get {
				return mSize;
			}
		}

		public override int Opacity {
			get {
				return (int)Format.Translucent;
			}
		}

		public float getProgress() {
			return this.mProgress;
		}

		public void setProgress(float progress) {
			this.mProgress = progress;
			InvalidateSelf ();
		}

		private static float lerp(float a, float b, float t)
		{
			return a + (b - a) * t;
		}
	}
}

