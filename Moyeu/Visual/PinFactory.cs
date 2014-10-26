using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

using XamSvg;

namespace Moyeu
{
	public class PinFactory
	{
		Dictionary<int, Bitmap> pinCache = new Dictionary<int, Bitmap> ();
		Bitmap closedPin;

		readonly Color baseLightGreenColor = Color.Rgb (0x99, 0xcc, 0x00);
		readonly Color baseLightRedColor = Color.Rgb (0xff, 0x44, 0x44);
		readonly Color baseDarkGreenColor = Color.Rgb (0x66, 0x99, 0x00);
		readonly Color baseDarkRedColor = Color.Rgb (0xcc, 0x00, 0x00);

		readonly Color fillColor = Color.Rgb (0x01, 0x02, 0x03);
		readonly Color bottomFillColor = Color.Rgb (0xaa, 0xbb, 0xcc);
		readonly Color borderColor = Color.Rgb (0xcf, 0xcf, 0xcf);

		Context context;

		Paint textPaint;

		public PinFactory (Context context)
		{
			this.context = context;
			this.textPaint = new Paint {
				TextSize = 12.ToPixels (),
				AntiAlias = true,
				TextAlign = Paint.Align.Center,
				Color = Color.White
			};
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

			var svg = SVGParser.ParseSVGFromResource (context.Resources,
			                                          Resource.Raw.pin,
			                                          SvgColorMapperFactory.FromFunc (c => ColorReplacer (c, ratio, alpha)));
			bmp = Bitmap.CreateBitmap (width, height, Bitmap.Config.Argb8888);
			using (var c = new Canvas (bmp)) {
				var dst = new RectF (0, 0, width, height); 
				c.DrawPicture (svg.Picture, dst);
				c.DrawText (number.ToString (), width / 2 - 1, 16.ToPixels (), textPaint);
			}

			pinCache [key] = bmp;
			return bmp;
		}

		public Bitmap GetClosedPin (int width, int height)
		{
			return closedPin ?? (closedPin = SvgFactory.GetBitmap (context.Resources, Resource.Raw.pin_locked, width, height));
		}

		Color ColorReplacer (Color inColor, float variant, float alpha)
		{
			Color tone = InterpolateColor (baseLightRedColor, baseLightGreenColor, variant);
			Color result = inColor;

			if (inColor == fillColor)
				result = tone;
			else if (inColor == borderColor)
				result = Lighten (tone, 0x20);
			else if (inColor == bottomFillColor)
				result = InterpolateColor (baseDarkRedColor, baseDarkGreenColor, variant);

			if (alpha < 1)
				return new Color ((byte)(result.R * alpha + 255 * (1 - alpha)),
				                  (byte)(result.G * alpha + 255 * (1 - alpha)),
				                  (byte)(result.B * alpha + 255 * (1 - alpha)));
			return result;
		}

		Color InterpolateColor (Color c1, Color c2, float ratio)
		{
			ratio = ExtraInterpolation (ratio);
			return Color.Rgb (c1.R + (int)((c2.R - c1.R) * ratio),
			                  c1.G + (int)((c2.G - c1.G) * ratio),
			                  c1.B + (int)((c2.B - c1.B) * ratio));
		}

		Color Lighten (Color baseColor, int increment)
		{
			return Color.Rgb (Math.Min (255, baseColor.R + increment),
			                  Math.Min (255, baseColor.G + increment),
			                  Math.Min (255, baseColor.B + increment));
		}

		float ExtraInterpolation (float ratio)
		{
			return ratio = 1 - (float)Math.Pow (1 - ratio, 4);
		}
	}
}

