using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using Android.Gms.Maps;
using Android.Gms.Maps.Model;

using FooSvg;

namespace Moyeu
{
	public class InfoWindowAdapter : Java.Lang.Object, GoogleMap.IInfoWindowAdapter
	{
		Context context;
		View view;
		FavoriteManager favManager;
		PictureBitmapDrawable bikeDrawable;
		PictureBitmapDrawable lockDrawable;
		PictureBitmapDrawable starOnDrawable;
		PictureBitmapDrawable starOffDrawable;

		public InfoWindowAdapter (Context context)
		{
			this.context = context;
			this.favManager = new FavoriteManager (context);
			bikeDrawable = SvgUtils.GetDrawableFromSvgRes (context.Resources, Resource.Raw.bike);
			lockDrawable = SvgUtils.GetDrawableFromSvgRes (context.Resources, Resource.Raw.ic_lock);
			starOnDrawable = SvgUtils.GetDrawableFromSvgRes (context.Resources, Resource.Raw.star_on);
			starOffDrawable = SvgUtils.GetDrawableFromSvgRes (context.Resources, Resource.Raw.star_off);
		}

		public int Id {
			get;
			private set;
		}

		public void ToggleStar ()
		{
			if (view == null)
				return;
			var starButton = view.FindViewById<ToggleButton> (Resource.Id.StarButton);
			if (starButton.Activated) {
				starButton.Activated = false;
				starButton.SetBackgroundDrawable (starOffDrawable);
			} else {
				starButton.Activated = true;
				starButton.SetBackgroundDrawable (starOnDrawable);
			}
		}

		public View GetInfoContents (Marker marker)
		{
			if (view == null) {
				var inflater = context.GetSystemService (Context.LayoutInflaterService).JavaCast<LayoutInflater> ();
				view = inflater.Inflate (Resource.Layout.InfoWindowLayout, null);
				var bikeView = view.FindViewById<ImageView> (Resource.Id.bikeImageView);
				var lockView = view.FindViewById<ImageView> (Resource.Id.lockImageView);
				bikeView.SetImageDrawable (bikeDrawable);
				lockView.SetImageDrawable (lockDrawable);
			}

			var name = view.FindViewById<TextView> (Resource.Id.InfoViewName);
			var bikes = view.FindViewById<TextView> (Resource.Id.InfoViewBikeNumber);
			var slots = view.FindViewById<TextView> (Resource.Id.InfoViewSlotNumber);
			var starButton = view.FindViewById<ToggleButton> (Resource.Id.StarButton); 

			var splitTitle = marker.Title.Split ('|');
			var displayName = splitTitle[1]
				.Split (new string[] { "-", " at " }, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault ();
			Id = int.Parse (splitTitle[0]);
			name.Text = (displayName ?? string.Empty).Trim ();
			var splitNumbers = marker.Snippet.Split ('|');
			bikes.Text = splitNumbers [0];
			slots.Text = splitNumbers [1];

			bool activated = favManager.GetFavoritesStationIds ().Contains (Id);
			starButton.Activated = activated;
			starButton.SetBackgroundDrawable (activated ? starOnDrawable : starOffDrawable);

			return view;
		}

		public View GetInfoWindow (Marker marker)
		{
			return null;
		}
	}
}

