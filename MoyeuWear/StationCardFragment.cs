
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

using Android.Support.Wearable;
using Android.Support.Wearable.Activity;
using Android.Support.Wearable.Views;

using MoyeuWear;

namespace Moyeu
{
	public class StationCardFragment : CardFragment
	{
		SimpleStation station;
		BikeActionStatus status;

		public override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			SetExpansionEnabled (false);
			SetCardGravity ((int)GravityFlags.Bottom);
		}

		public override View OnCreateContentView (LayoutInflater inflater, ViewGroup container, Bundle state)
		{
			return inflater.Inflate (Resource.Layout.StationCardLayout, container, false);
		}

		public override void OnViewCreated (View view, Bundle savedInstanceState)
		{
			SetCountText (view);

			view.FindViewById<TextView> (Resource.Id.stationPrimary).Text = station.Primary;
			view.FindViewById<TextView> (Resource.Id.stationSecondary).Text = station.Secondary;

			// station.Distance is km, we just convert to miles to not disturb our American imperialist friends
			var distance = station.Distance * 0.62137;
			var distanceText = view.FindViewById<TextView> (Resource.Id.distance);
			if (distance < 0.1875)
				distanceText.Text = (distance * 5280).ToString ("N0") + " feet";
			else
				distanceText.Text = distance.ToString ("N1") + " mi";
		}

		public void SwitchCount ()
		{
			status = status == BikeActionStatus.Stop ? BikeActionStatus.Start : BikeActionStatus.Stop;
			if (View != null)
				SetCountText (View);
		}

		void SetCountText (View view)
		{
			var count = status == BikeActionStatus.Start ? station.Bikes : station.Racks;
			var ratio = ((float)count) / (station.Racks + station.Bikes);
			var color = InterpolateColor (Color.Rgb (0xff, 0x44, 0x44),
			                              Color.Rgb (0x99, 0xcc, 0x00),
			                              ratio);
			var bg = new RoundRectDrawable (color, TypedValue.ApplyDimension (ComplexUnitType.Dip, 2, view.Resources.DisplayMetrics));
			var countText = View.FindViewById<TextView> (Resource.Id.count);
			countText.Text = count.ToString ();
			((View)countText.Parent).SetBackgroundDrawable (bg);
		}

		public static StationCardFragment WithStation (SimpleStation station, BikeActionStatus status)
		{
			var r = new StationCardFragment ();
			r.station = station;
			r.status = status;
			return r;
		}

		static Color InterpolateColor (Color c1, Color c2, float ratio)
		{
			ratio = ExtraInterpolation (ratio);
			return Color.Rgb (c1.R + (int)((c2.R - c1.R) * ratio),
			                  c1.G + (int)((c2.G - c1.G) * ratio),
			                  c1.B + (int)((c2.B - c1.B) * ratio));
		}

		static float ExtraInterpolation (float ratio)
		{
			return ratio = 1 - (float)Math.Pow (1 - ratio, 4);
		}
	}
}

