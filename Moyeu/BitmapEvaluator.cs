using System;

using Android.Animation;
using Android.Graphics;
using Java.Lang;

namespace Moyeu
{
	public class BitmapEvaluator : Java.Lang.Object, ITypeEvaluator
	{
		Bitmap cacheBitmap;
		Paint blendPaint;

		public Java.Lang.Object Evaluate (float fraction, Java.Lang.Object startValue, Java.Lang.Object endValue)
		{
			var bmp1 = startValue as Bitmap;
			var bmp2 = endValue as Bitmap;
			if (cacheBitmap == null)
				cacheBitmap = Bitmap.CreateBitmap (bmp1.Width, bmp1.Height, Bitmap.Config.Argb8888);
			if (blendPaint == null) {
				blendPaint = new Paint ();
				blendPaint.SetXfermode (new PorterDuffXfermode (PorterDuff.Mode.SrcOver));
			}

			using (var canvas = new Canvas (cacheBitmap)) {
				canvas.DrawBitmap (bmp1, 0, 0, null);
				blendPaint.Alpha = (int)((1 - fraction) * 255);
				canvas.DrawBitmap (bmp2, 0, 0, blendPaint);
			}
			return cacheBitmap;
		}
	}
}

