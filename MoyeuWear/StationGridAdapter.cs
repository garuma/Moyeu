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
using Android.Graphics;

using Android.Support.Wearable;
using Android.Support.Wearable.Activity;
using Android.Support.Wearable.Views;

using MoyeuWear;

namespace Moyeu
{
	public class StationGridAdapter : FragmentGridPagerAdapter
	{
		IList<SimpleStation> stations;
		IMoyeuActions actions;
		StationCardFragment[] stationFragments;

		// We alternate between two version of the same background so that the parallax doesn't get confused
		ImageReference background, background2;

		public StationGridAdapter (FragmentManager manager,
		                           IList<SimpleStation> stations,
		                           IMoyeuActions actions)
			: base (manager)
		{
			this.stations = stations;
			this.actions = actions;
			this.stationFragments = new StationCardFragment[stations.Count];
			this.background = ImageReference.ForDrawable (Resource.Drawable.pager_background);
			this.background2 = ImageReference.ForDrawable (Resource.Drawable.pager_background2);
		}

		public void SwitchCount ()
		{
			foreach (var frag in stationFragments)
				if (frag != null)
					frag.SwitchCount ();
		}

		public override int RowCount {
			get {
				return stations.Count;
			}
		}

		public override ImageReference GetBackground (int row, int column)
		{
			var station = stations [row];
			return station.Background != null ? ImageReference.ForBitmap (station.Background)
				: (row % 2) == 0 ? background : background2;
		}

		public override int GetColumnCount (int p0)
		{
			// The data item and two actions (navigate, favorite)
			return 3;
		}

		public override Fragment GetFragment (int row, int column)
		{
			var station = stations [row];
			var id = station.Id;

			if (column == 1)
				return ActionButtonFragment.WithAction ("Navigate", Resource.Drawable.navigate_button,
				                                        () => actions.NavigateToStation (id));
			if (column == 2)
				return ActionButtonFragment.WithToggleAction (Tuple.Create ("Favorite", "Unfavorite"),
				                                              Resource.Drawable.favorite_button,
				                                              station.IsFavorite,
				                                              cked => actions.ToggleFavoriteStation (id, cked));

			return stationFragments [row] ??
				(stationFragments [row] = StationCardFragment.WithStation (station, actions.CurrentStatus));
		}
	}
}

