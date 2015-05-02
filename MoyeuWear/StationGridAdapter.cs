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
using Android.Graphics.Drawables;

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
		Drawable[] backgrounds;

		public StationGridAdapter (FragmentManager manager,
		                           IList<SimpleStation> stations,
		                           IMoyeuActions actions)
			: base (manager)
		{
			this.stations = stations;
			this.actions = actions;
			this.stationFragments = new StationCardFragment[stations.Count];
			this.backgrounds = new Drawable[stations.Count];
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

		public override Drawable GetBackgroundForRow (int row)
		{
			if (backgrounds [row] != null)
				return backgrounds [row];

			var station = stations [row];
			var bg = station.Background != null ?
				new ShadowedBitmapDrawable (station.Background) { ShadowColor = Color.Argb (0x80, 0, 0, 0) } : 
				GridPagerAdapter.BackgroundNone;
			backgrounds [row] = bg;
			return bg;
		}

		public override Drawable GetBackgroundForPage (int p0, int p1)
		{
			return GridPagerAdapter.BackgroundNone;
		}

		public override int GetColumnCount (int p0)
		{
			// The data item and two actions (navigate, favorite)
			return 3;
		}

		public override int GetOptionsForPage (int row, int column)
		{
			return column > 0 ? GridPagerAdapter.OptionDisableParallax : GridPagerAdapter.PageDefaultOptions;
		}

		public override Fragment GetFragment (int row, int column)
		{
			var station = stations [row];
			var id = station.Id;

			if (column == 1)
				return ActionButtonFragment.WithAction ("Navigate", Resource.Drawable.navigate_button,
				                                        () => actions.NavigateToStation (station));
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

