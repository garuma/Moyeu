using System;

using Android.Graphics;

namespace Moyeu
{
	public class BitmapCache
	{
		DiskCache diskCache;
		LRUCache<string, Bitmap> memCache;

		BitmapCache (DiskCache diskCache)
		{
			this.diskCache = diskCache;
			this.memCache = new LRUCache<string, Bitmap> (10);
		}

		public static BitmapCache CreateCache (Android.Content.Context ctx, string cacheName, string version = "1.0")
		{
			return new BitmapCache (DiskCache.CreateCache (ctx, cacheName, version));
		}

		public void AddOrUpdate (string key, Bitmap bmp, TimeSpan duration)
		{
			diskCache.AddOrUpdate (key, bmp, duration);
			memCache.Put (key, bmp);
		}

		public bool TryGet (string key, out Bitmap bmp)
		{
			if ((bmp = memCache.Get (key)) != null)
				return true;
			return diskCache.TryGet (key, out bmp);
		}
	}
}

