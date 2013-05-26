using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Locations;
using Android.Database;
using Android.Net;
using Android.Provider;
using Android.OS;

namespace Moyeu
{
	[ContentProvider (new string[] { "org.neteril.AddressSuggestionsProvider" },
	                  Name = "org.neteril.AddressSuggestionsProvider",
	                  Exported = false)]
	public class AddressSuggestionsProvider : ContentProvider
	{
		public const double LowerLeftLat = 42.343828;
		public const double LowerLeftLon = -71.169777;
		public const double UpperRightLat = 42.401150;
		public const double UpperRightLon = -71.017685;

		const int SuggestionCount = 7;

		Geocoder geocoder;

		public override bool OnCreate ()
		{
			this.geocoder = new Geocoder (Context);
			return true;
		}

		public override ICursor Query (Android.Net.Uri uri, string[] projection, string selection, string[] selectionArgs, string sortOrder)
		{
			var query = uri.LastPathSegment.ToLowerInvariant ();
			var addresses = geocoder.GetFromLocationName (query,
			                                              SuggestionCount,
			                                              LowerLeftLat,
			                                              LowerLeftLon,
			                                              UpperRightLat,
			                                              UpperRightLon);

			var cursor = new MatrixCursor (new string[] {
				BaseColumns.Id,
				SearchManager.SuggestColumnText1,
				SearchManager.SuggestColumnText2,
				SearchManager.SuggestColumnIntentExtraData
			}, addresses.Count);

			long id = 0;

			foreach (var address in addresses) {
				int dummy;
				if (int.TryParse (address.Thoroughfare, out dummy))
					continue;

				var options1 = new string[] { address.FeatureName, address.Thoroughfare };
				var options2 = new string[] { address.Locality, address.AdminArea };

				var line1 = string.Join (", ", options1.Where (s => !string.IsNullOrEmpty (s)).Distinct ());
				var line2 = string.Join (", ", options2.Where (s => !string.IsNullOrEmpty (s)));

				if (string.IsNullOrEmpty (line1) || string.IsNullOrEmpty (line2))
					continue;

				cursor.AddRow (new Java.Lang.Object[] {
					id++,
					line1,
					line2,
					address.Latitude + "|" + address.Longitude
				});
			}

			return cursor;
		}

		public override string GetType (Android.Net.Uri uri)
		{
			return null;
		}

		public override int Delete (Android.Net.Uri uri, string selection, string[] selectionArgs)
		{
			throw new NotImplementedException ();
		}

		public override Android.Net.Uri Insert (Android.Net.Uri uri, ContentValues values)
		{
			throw new NotImplementedException ();
		}

		public override int Update (Android.Net.Uri uri, ContentValues values, string selection, string[] selectionArgs)
		{
			throw new NotImplementedException ();
		}
	}
}

