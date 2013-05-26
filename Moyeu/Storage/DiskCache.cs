using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Bitmap = Android.Graphics.Bitmap;
using Env = Android.OS.Environment;

namespace Moyeu
{
	public class DiskCache
	{
		enum JournalOp {
			Created = 'c',
			Modified = 'm',
			Deleted = 'd'
		}

		const string JournalFileName = ".journal";
		const string Magic = "MONOID";
		readonly Encoding encoding = Encoding.UTF8;
		string basePath;
		string journalPath;
		string version;
		//Timer cleanupTimer;

		struct CacheEntry
		{
			public DateTime Origin;
			public TimeSpan TimeToLive;

			public CacheEntry (DateTime o, TimeSpan ttl)
			{
				Origin = o;
				TimeToLive = ttl;
			}
		}

		Dictionary<string, CacheEntry> entries = new Dictionary<string, CacheEntry> ();

		public DiskCache (string basePath, string version)
		{
			this.basePath = basePath;
			if (!Directory.Exists (basePath))
				Directory.CreateDirectory (basePath);
			this.journalPath = Path.Combine (basePath, JournalFileName);
			this.version = version;

			try {
				InitializeWithJournal ();
			} catch {
				Directory.Delete (basePath, true);
				Directory.CreateDirectory (basePath);
			}

			ThreadPool.QueueUserWorkItem (CleanCallback);
		}

		public static DiskCache CreateCache (Android.Content.Context ctx, string cacheName, string version = "1.0")
		{
			string cachePath = ctx.CacheDir.AbsolutePath;
			return new DiskCache (Path.Combine (cachePath, cacheName), version);
		}

		void InitializeWithJournal ()
		{
			if (!File.Exists (journalPath)) {
				using (var writer = new StreamWriter (journalPath, false, encoding)) {
					writer.WriteLine (Magic);
					writer.WriteLine (version);
				}
				return;
			}

			string line = null;
			using (var reader = new StreamReader (journalPath, encoding)) {
				if (!EnsureHeader (reader))
					throw new InvalidOperationException ("Invalid header");
				while ((line = reader.ReadLine ()) != null) {
					try {
						var op = ParseOp (line);
						string key;
						DateTime origin;
						TimeSpan duration;

						switch (op) {
						case JournalOp.Created:
							ParseEntry (line, out key, out origin, out duration);
							entries.Add (key, new CacheEntry (origin, duration));
							break;
						case JournalOp.Modified:
							ParseEntry (line, out key, out origin, out duration);
							entries[key] = new CacheEntry (origin, duration);
							break;
						case JournalOp.Deleted:
							ParseEntry (line, out key);
							entries.Remove (key);
							break;
						}
					} catch {
						break;
					}
				}
			}
		}

		void CleanCallback (object state)
		{
			KeyValuePair<string, CacheEntry>[] kvps;
			lock (entries) {
				var now = DateTime.UtcNow;
				kvps = entries.Where (kvp => kvp.Value.Origin + kvp.Value.TimeToLive < now).Take (10).ToArray ();
				Android.Util.Log.Info ("DiskCacher", "Removing {0} elements from the cache", kvps.Length);
				foreach (var kvp in kvps) {
					entries.Remove (kvp.Key);
					try {
						File.Delete (Path.Combine (basePath, kvp.Key));
					} catch {}
				}
			}
		}

		bool EnsureHeader (StreamReader reader)
		{
			var m = reader.ReadLine ();
			var v = reader.ReadLine ();

			return m == Magic && v == version;
		}

		JournalOp ParseOp (string line)
		{
			return (JournalOp)line[0];
		}

		void ParseEntry (string line, out string key)
		{
			key = line.Substring (2);
		}

		void ParseEntry (string line, out string key, out DateTime origin, out TimeSpan duration)
		{
			key = null;
			origin = DateTime.MinValue;
			duration = TimeSpan.MinValue;

			var parts = line.Substring (2).Split (' ');
			if (parts.Length != 3)
				throw new InvalidOperationException ("Invalid entry");
			key = parts[0];

			long dateTime, timespan;

			if (!long.TryParse (parts[1], out dateTime))
				throw new InvalidOperationException ("Corrupted origin");
			else
				origin = new DateTime (dateTime);

			if (!long.TryParse (parts[2], out timespan))
				throw new InvalidOperationException ("Corrupted duration");
			else
				duration = TimeSpan.FromMilliseconds (timespan);
		}

		public void AddOrUpdate (string key, Bitmap bmp, TimeSpan duration)
		{
			key = SanitizeKey (key);
			lock (entries) {
				bool existed = entries.ContainsKey (key);
				using (var stream = new BufferedStream (File.OpenWrite (Path.Combine (basePath, key))))
					bmp.Compress (Bitmap.CompressFormat.Png, 100, stream);
				AppendToJournal (existed ? JournalOp.Modified : JournalOp.Created,
				                 key,
				                 DateTime.UtcNow,
				                 duration);
				entries[key] = new CacheEntry (DateTime.UtcNow, duration);
			}
		}

		public bool TryGet (string key, out Bitmap bmp)
		{
			key = SanitizeKey (key);
			lock (entries) {
				bmp = null;
				if (!entries.ContainsKey (key))
					return false;
				try {
					bmp = Android.Graphics.BitmapFactory.DecodeFile (Path.Combine (basePath, key));
				} catch {
					return false;
				}

				return true;
			}
		}

		void AppendToJournal (JournalOp op, string key)
		{
			using (var writer = new StreamWriter (journalPath, true, encoding)) {
				writer.Write ((char)op);
				writer.Write (' ');
				writer.Write (key);
				writer.WriteLine ();
			}
		}

		void AppendToJournal (JournalOp op, string key, DateTime origin, TimeSpan ttl)
		{
			using (var writer = new StreamWriter (journalPath, true, encoding)) {
				writer.Write ((char)op);
				writer.Write (' ');
				writer.Write (key);
				writer.Write (' ');
				writer.Write (origin.Ticks);
				writer.Write (' ');
				writer.Write ((long)ttl.TotalMilliseconds);
				writer.WriteLine ();
			}
		}

		string SanitizeKey (string key)
		{
			return new string (key
			                   .Where (c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
			                   .ToArray ());
		}
	}
}

