using System;

using Android.Hardware;
using Android.Util;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Graphics.Drawables;

namespace Moyeu
{
	public static class AndroidExtensions
	{
		static float density;

		public static void Initialize (Context context)
		{
			var wm = context.GetSystemService (Context.WindowService).JavaCast<IWindowManager> ();
			var displayMetrics = new DisplayMetrics ();
			wm.DefaultDisplay.GetMetrics (displayMetrics);
			density = displayMetrics.Density;

			var bg = new TypedValue ();
			context.Theme.ResolveAttribute (Android.Resource.Attribute.ColorBackground, bg, true);
			DefaultBackground = new ColorDrawable (new Android.Graphics.Color (bg.Data));
		}

		public static int ToPixels (this int dp)
		{
			return (int)(dp * density + 0.5f);
		}

		public static ColorDrawable DefaultBackground { get; private set; }
	}
}

