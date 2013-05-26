using System;
using System.Linq;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace Moyeu
{
	public class FavoriteManager
	{
		const string FavoriteKey = "favorites";

		Context context;
		ISharedPreferences prefs;

		public FavoriteManager (Context context)
		{
			this.context = context;
		}

		ISharedPreferences Preferences {
			get {
				return prefs ?? (prefs = context.GetSharedPreferences ("preferences", FileCreationMode.Private));
			}
		}

		public HashSet<int> GetFavoritesStationIds ()
		{
			var favs = Preferences.GetStringSet (FavoriteKey, new string[0]);
			return new HashSet<int> (favs.Select (f => int.Parse (f)));
		}

		public void AddToFavorite (int id)
		{
			var existingIds = GetFavoritesStationIds ();
			existingIds.Add (id);
			var editor = Preferences.Edit ();
			editor.PutStringSet (FavoriteKey, existingIds.Select (i => i.ToString ()).ToArray ());
			editor.Commit ();
		}

		public void RemoveFromFavorite (int id)
		{
			var existingIds = GetFavoritesStationIds ();
			existingIds.Remove (id);
			var editor = Preferences.Edit ();
			editor.PutStringSet (FavoriteKey, existingIds.Select (i => i.ToString ()).ToArray ());
			editor.Commit ();
		}
	}
}

