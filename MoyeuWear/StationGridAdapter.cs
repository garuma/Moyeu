using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using Android.Support.Wearable;
using Android.Support.Wearable.Activity;
using Android.Support.Wearable.Views;

using MoyeuWear;

namespace Moyeu
{
	public class StationGridAdapter : FragmentGridPagerAdapter
	{
		IList<Station> stations;
		GeoPoint currentLocation;
		BikeActionStatus status;

		// We alternate between two version of the same background so that the parallax doesn't get confused
		ImageReference background, background2;

		public StationGridAdapter (FragmentManager manager, IList<Station> stations, GeoPoint currentLocation, BikeActionStatus status)
			: base (manager)
		{
			this.stations = stations;
			this.currentLocation = currentLocation;
			this.status = status;
			this.background = ImageReference.ForDrawable (Resource.Drawable.pager_background);
			this.background2 = ImageReference.ForDrawable (Resource.Drawable.pager_background2);
		}

		public override int RowCount {
			get {
				return stations.Count;
			}
		}

		public override ImageReference GetBackground (int row, int column)
		{
			return (row % 2) == 0 ? background : background2;
		}

		public override int GetColumnCount (int p0)
		{
			// The data item and two actions (navigate, favorite)
			return 3;
		}

		public override Fragment GetFragment (int row, int column)
		{
			if (column == 1)
				return ActionButtonFragment.WithAction ("Navigate", Resource.Drawable.navigate_button);
			if (column == 2)
				return ActionButtonFragment.WithAction ("Add to Favorite", Resource.Drawable.favorite_button);

			return StationCardFragment.WithStation (stations [row], currentLocation, status);
		}
	}
}

