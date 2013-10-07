using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace Moyeu
{
	public class FavoriteManager
	{
		const string FavoriteKey = "favoritesString";

		Context context;
		ISharedPreferences prefs;

		public static event EventHandler FavoritesChanged;

		static FavoriteManager instance;

		public static FavoriteManager Obtain (Context context)
		{
			return instance ?? (instance = new FavoriteManager (context));
		}

		private FavoriteManager (Context context)
		{
			this.context = context;
		}

		ISharedPreferences Preferences {
			get {
				return prefs ?? (prefs = context.GetSharedPreferences ("preferences", FileCreationMode.Private));
			}
		}

		public HashSet<int> LastFavorites {
			get;
			private set;
		}

		public HashSet<int> GetFavoriteStationIds ()
		{
			var favs = Preferences.GetString (FavoriteKey, string.Empty);
			var result = new HashSet<int> (favs.Split (',')
			                               .Where (f => !string.IsNullOrEmpty (f))
			                               .Select (f => int.Parse (f)));
			LastFavorites = result;
			return result;
		}

		public Task<HashSet<int>> GetFavoriteStationIdsAsync ()
		{
			return Task.Run (new Func<HashSet<int>> (GetFavoriteStationIds));
		}

		public void AddToFavorite (int id)
		{
			var existingIds = GetFavoriteStationIds ();
			existingIds.Add (id);
			var editor = Preferences.Edit ();
			editor.PutString (FavoriteKey, string.Join (",", existingIds.Select (i => i.ToString ())));
			editor.Commit ();
			if (FavoritesChanged != null)
				FavoritesChanged (this, EventArgs.Empty);
		}

		public void RemoveFromFavorite (int id)
		{
			var existingIds = GetFavoriteStationIds ();
			existingIds.Remove (id);
			var editor = Preferences.Edit ();
			editor.PutString (FavoriteKey, string.Join (",", existingIds.Select (i => i.ToString ())));
			editor.Commit ();
			if (FavoritesChanged != null)
				FavoritesChanged (this, EventArgs.Empty);
		}
	}
}

