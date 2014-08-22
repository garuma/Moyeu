
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
using Android.Graphics;
using Android.Graphics.Drawables;

namespace MoyeuWear
{
	public class RoundRectDrawable : Drawable
	{
		float radius;
		Paint paint;
		RectF bounds;

		public RoundRectDrawable (Color background, float radius)
		{
			this.radius = radius;
			this.paint = new Paint {
				Color = background,
				Dither = true,
				AntiAlias = true
			};
			this.bounds = new RectF ();
		}

		public override void Draw (Canvas canvas)
		{
			canvas.DrawRoundRect (bounds, radius, radius, paint);
		}

		protected override void OnBoundsChange (Rect bounds)
		{
			base.OnBoundsChange (bounds);
			this.bounds.Set (bounds);
		}

		public Color Background {
			get { return paint.Color; }
			set {
				paint.Color = value;
				InvalidateSelf ();
			}
		}

		public float Radius {
			get { return radius; }
			set {
				radius = value;
				InvalidateSelf ();
			}
		}

		public override int Alpha {
			get {
				return 1;
			}
			set {
			}
		}

		public override void SetAlpha (int alpha)
		{
		}

		public override void SetColorFilter (ColorFilter cf)
		{
		}

		public override int Opacity {
			get {
				return (int)Format.Opaque;
			}
		}
	}
}

