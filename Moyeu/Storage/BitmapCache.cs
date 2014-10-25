using System;

using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

using Rdio.TangoAndCache.Android.UI.Drawables;
using Rdio.TangoAndCache.Android.Collections;

namespace Moyeu
{
	public class BitmapCache
	{
		Context context;
		bool useRoundCorners;

		DiskCache diskCache;
		ReuseBitmapDrawableCache memCache;

		BitmapCache (Context ctx, DiskCache diskCache, bool useRoundCorners)
		{
			this.context = ctx;
			this.useRoundCorners = useRoundCorners;
			this.diskCache = diskCache;
			var highWatermark = Java.Lang.Runtime.GetRuntime().MaxMemory() / 3;
			var lowWatermark = highWatermark / 2;
			this.memCache = new ReuseBitmapDrawableCache (highWatermark, lowWatermark, highWatermark);
		}

		public static BitmapCache CreateCache (Context ctx, string cacheName, string version = "1.0", bool useRoundCorners = true)
		{
			return new BitmapCache (ctx, DiskCache.CreateCache (ctx, cacheName, version), useRoundCorners);
		}

		public SelfDisposingBitmapDrawable AddOrUpdate (string key, Bitmap bmp, TimeSpan duration)
		{
			diskCache.AddOrUpdate (key, bmp, duration);
			var drawable = CreateCacheableDrawable (bmp);
			memCache.Add (new Uri (key), drawable);
			return drawable;
		}

		public bool TryGet (string key, out SelfDisposingBitmapDrawable drawable)
		{
			if (memCache.TryGetValue (new Uri (key), out drawable))
				return true;

			Bitmap bmp;
			if (!diskCache.TryGet (key, out bmp))
				return false;

			drawable = CreateCacheableDrawable (bmp);
			return true;
		}

		SelfDisposingBitmapDrawable CreateCacheableDrawable (Bitmap bmp)
		{
			var res = context.Resources;
			return useRoundCorners ? new RoundCornerDrawable (res, bmp) : new SelfDisposingBitmapDrawable (res, bmp);
		}
	}
}

