using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace Moyeu
{
	public class PinFactory
	{
		Dictionary<int, Bitmap> pinCache = new Dictionary<int, Bitmap> ();
		Bitmap closedPin;

		readonly Color baseLightGreenColor;
		readonly Color baseLightRedColor;
		readonly Color baseDarkGreenColor;
		readonly Color baseDarkRedColor;

		Paint textPaint, pinPaint, lockPaint;
		Path pinPath, lockPath;

		readonly int textSizeOneDigit, textSizeTwoDigits;
		RectF pathBounds = new RectF ();

		public PinFactory ()
		{
			this.textPaint = new Paint {
				AntiAlias = true,
				TextAlign = Paint.Align.Center,
				Color = Color.White,
			};
			textSizeOneDigit = 13.ToPixels ();
			textSizeTwoDigits = 12.ToPixels ();
			this.pinPaint = new Paint { AntiAlias = true };
			this.lockPaint = new Paint {
				AntiAlias = true,
				Color = Color.White,
			};
			lockPaint.SetStyle (Paint.Style.Fill);

			pinPath = new Path ();
			pinPath.MoveTo (12, 1);
			pinPath.RCubicTo (-4.246f, 0, -7.7f, 3.454f, -7.7f, 7.7f);
			pinPath.RCubicTo (0, 5.775f, 7.7f, 14.3f, 7.7f, 14.3f);
			pinPath.RCubicTo (0, 0, 7.7f, -8.525f, 7.7f, -14.3f);
			pinPath.RCubicTo (0, -4.246f, -3.454f, -7.7f, -7.7f, -7.7f);
			pinPath.Close ();

			lockPath = new Path ();
			lockPath.MoveTo (18, 8);
			lockPath.RLineTo (-1, 0);
			lockPath.RLineTo (0, -2);
			lockPath.RCubicTo (0, -2.76f, -2.24f, -5, -5, -5);
			lockPath.RCubicTo (-2.76f, 0, - 5, 2.24f, -5, 5);
			lockPath.RLineTo (0, 2);
			lockPath.RLineTo (-1, 0);
			lockPath.RCubicTo (-1.1f, 0, -2, 0.9f, -2, 2);
			lockPath.RLineTo (0, 10);
			lockPath.RCubicTo (0,1.1f, 0.9f,2, 2,2);
			lockPath.RLineTo (12,0);
			lockPath.RCubicTo (1.1f,0, 2,-0.9f, 2,-2);
			lockPath.RLineTo (0,-10);
			lockPath.RCubicTo (0,-1.1f, -0.9f,-2, -2,-2);
			lockPath.Close ();
			lockPath.MoveTo (12,2.9f);
			lockPath.RCubicTo (1.71f,0, 3.1f,1.39f, 3.1f,3.1f);
			lockPath.RLineTo (0,2);
			lockPath.RLineTo (-6.1f,0);
			lockPath.RLineTo (0,-2);
			lockPath.RCubicTo (0,-1.71f, 1.29f,-3.1f, 3,-3.1f);
			lockPath.RLineTo (0, 0);
			lockPath.Close ();

			baseLightGreenColor = Color.Rgb (0x99, 0xcc, 0x00);
			baseLightRedColor = Color.Rgb (0xff, 0x44, 0x44);
			baseDarkGreenColor = Color.Rgb (0x66, 0x99, 0x00);
			baseDarkRedColor = Color.Rgb (0xcc, 0x00, 0x00);
		}

		public Task<Bitmap> GetPinAsync (float ratio, int number, int width, int height, float alpha = 1)
		{
			return Task.Run (() => GetPin (ratio, number, width, height, alpha));
		}

		public Bitmap GetPin (float ratio, int number, int width, int height, float alpha = 1)
		{
			int key = number + ((int)(ratio * 10000)) << 6;
			Bitmap bmp;
			if (pinCache.TryGetValue (key, out bmp))
				return bmp;

			Color tone = InterpolateColor (baseLightRedColor, baseLightGreenColor, ratio);
			Color toneDark = InterpolateColor (baseDarkRedColor, baseDarkGreenColor, ratio);

			bmp = Bitmap.CreateBitmap (width, height, Bitmap.Config.Argb8888);
			using (var c = new Canvas (bmp)) {
				c.Save ();
				c.Scale (width / 24f, height / 24f, 0, 0);
				using (var gradient = new LinearGradient (0, 0, 0, height, tone, toneDark, Shader.TileMode.Clamp)) {
					pinPaint.SetStyle (Paint.Style.Fill);
					pinPaint.SetShader (gradient);
					c.DrawPath (pinPath, pinPaint);
					pinPaint.SetShader (null);
				}
				pinPaint.SetStyle (Paint.Style.Stroke);
				pinPaint.StrokeWidth = .5f;
				pinPaint.Color = toneDark;
				c.DrawPath (pinPath, pinPaint);

				c.Restore ();

				var text = number.ToString ();
				textPaint.TextSize = text.Length == 1 ? textSizeOneDigit : textSizeTwoDigits;
				c.DrawText (text, width / 2f - .5f, 16.ToPixels (), textPaint);
			}

			pinCache [key] = bmp;
			return bmp;
		}

		public Bitmap GetClosedPin (int width, int height)
		{
			if (closedPin != null)
				return closedPin;

			Color borderColor = Color.Rgb (0, 0x71, 0x96);
			Color bottomShade = Color.Rgb (0, 0x99, 0xCC);
			Color topShade = Color.Rgb (0, 0xB6, 0xF4);

			closedPin = Bitmap.CreateBitmap (width, height, Bitmap.Config.Argb8888);
			using (var c = new Canvas (closedPin)) {
				c.Save ();
				c.Scale (width / 24f, height / 24f, 0, 0);
				using (var gradient = new LinearGradient (0, 0, 0, height, topShade, bottomShade, Shader.TileMode.Clamp)) {
					pinPaint.SetStyle (Paint.Style.Fill);
					pinPaint.SetShader (gradient);
					c.DrawPath (pinPath, pinPaint);
					pinPaint.SetShader (null);
				}
				pinPaint.SetStyle (Paint.Style.Stroke);
				pinPaint.StrokeWidth = .5f;
				pinPaint.Color = borderColor;
				
				c.DrawPath (pinPath, pinPaint);

				c.Save ();
				c.Scale (.5f, .5f, 0, 0);
				c.Translate (12, 6);
				c.DrawPath (lockPath, lockPaint);
				c.Restore ();
				c.Restore ();
			}

			return closedPin;
		}

		public static Color InterpolateColor (Color c1, Color c2, float ratio)
		{
			ratio = ExtraInterpolation (ratio);
			return Color.Rgb (c1.R + (int)((c2.R - c1.R) * ratio),
			                  c1.G + (int)((c2.G - c1.G) * ratio),
			                  c1.B + (int)((c2.B - c1.B) * ratio));
		}

		static Color Lighten (Color baseColor, int increment)
		{
			return Color.Rgb (Math.Min (255, baseColor.R + increment),
			                  Math.Min (255, baseColor.G + increment),
			                  Math.Min (255, baseColor.B + increment));
		}

		static float ExtraInterpolation (float ratio)
		{
			return ratio = 1 - (float)Math.Pow (1 - ratio, 4);
		}
	}
}

