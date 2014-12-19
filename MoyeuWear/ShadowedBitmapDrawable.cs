using System;

using Android.Graphics;
using Android.Graphics.Drawables;

namespace MoyeuWear
{
	public class ShadowedBitmapDrawable : BitmapDrawable
	{
		float shadowLevel;

		public ShadowedBitmapDrawable (Bitmap bmp) : base (bmp)
		{

		}

		public Color ShadowColor {
			get;
			set;
		}

		public void SetShadowLevel (float level)
		{
			this.shadowLevel = level;
			InvalidateSelf ();
		}

		public override void Draw (Canvas canvas)
		{
			base.Draw (canvas);
			var shadow = ShadowColor;
			var newAlpha = (byte)(shadow.A * shadowLevel);
			shadow.A = newAlpha;
			canvas.DrawColor (shadow);
		}
	}
}

