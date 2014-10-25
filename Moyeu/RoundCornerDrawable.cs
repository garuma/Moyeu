using System;

using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Content.Res;

using Rdio.TangoAndCache.Android.UI.Drawables;

namespace Moyeu
{
	public class RoundCornerDrawable : SelfDisposingBitmapDrawable
	{
		float mCornerRadius;
		RectF mRect = new RectF ();
		BitmapShader bitmapShader;
		Paint paint;
		Paint strokePaint;
		int margin;

		public RoundCornerDrawable (Resources res, Bitmap bitmap, float cornerRadius = 5, int margin = 3)
			: base (res, bitmap)
		{
			mCornerRadius = cornerRadius;

			bitmapShader = new BitmapShader (bitmap, Shader.TileMode.Clamp, Shader.TileMode.Clamp);

			paint = new Paint () {
				AntiAlias = true,
			};
			paint.SetShader (bitmapShader);
			strokePaint = new Paint () {
				Color = Color.Rgb (200, 200, 200),
				StrokeWidth = 2,
			};
			strokePaint.SetStyle (Paint.Style.Stroke);

			this.margin = margin;
		}

		protected override void OnBoundsChange (Rect bounds)
		{
			base.OnBoundsChange (bounds);
			mRect.Set (margin, margin, bounds.Width () - margin, bounds.Height () - margin);
		}

		public override void Draw (Canvas canvas)
		{
			canvas.DrawRoundRect (mRect, mCornerRadius, mCornerRadius, strokePaint);
			canvas.DrawRoundRect (mRect, mCornerRadius, mCornerRadius, paint);
		}

		public override int Opacity {
			get {
				return (int)Format.Translucent;
			}
		}

		public override void SetAlpha (int alpha)
		{
			paint.Alpha = alpha;
		}

		public override void SetColorFilter (ColorFilter cf)
		{
			paint.SetColorFilter (cf);
		}
	}
}

