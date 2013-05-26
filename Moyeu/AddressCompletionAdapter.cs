using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Locations;

namespace Moyeu
{
	class AddressCompletionAdapter: BaseAdapter, IFilterable
	{
		class AddressFilter : Filter
		{
			const double LowerLeftLat = 42.30548;
			const double LowerLeftLon = -71.199552;
			const double UpperRightLat = 42.426732;
			const double UpperRightLon = -70.93176;

			AddressCompletionAdapter adapter;
			Geocoder geocoder;

			public AddressFilter (Context context, AddressCompletionAdapter adapter)
			{
				this.adapter = adapter;
				this.geocoder = new Geocoder (context);
			}

			protected override FilterResults PerformFiltering (Java.Lang.ICharSequence constraint)
			{
				if (constraint == null)
					return new FilterResults () { Count = 0, Values = null };
				var search = constraint.ToString ();
				var addresses = geocoder.GetFromLocationName (search, 7,
				                                              LowerLeftLat,
				                                              LowerLeftLon,
				                                              UpperRightLat,
				                                              UpperRightLon);
				return new FilterResults () {
					Count = addresses.Count,
					Values = (Java.Lang.Object)addresses
				};
			}

			protected override void PublishResults (Java.Lang.ICharSequence constraint, FilterResults results)
			{
				adapter.SetItems (results.Count == 0 ? null : (IList<Address>)results.Values);
			}
		}

		Context context;
		AddressFilter filter;
		IList<Address> backingStore;

		public AddressCompletionAdapter (Context context)
		{
			this.context = context;
			this.filter = new AddressFilter (context, this);
		}

		public void SetItems (IList<Address> data)
		{
			backingStore = data;
			NotifyDataSetChanged ();
		}

		public Filter Filter {
			get {
				return filter;
			}
		}

		public override Java.Lang.Object GetItem (int position)
		{
			var address = backingStore [position];
			var options = new string[] { address.FeatureName, address.Thoroughfare, address.Locality, address.CountryName };
			return string.Join (", ", options.Where (s => !string.IsNullOrEmpty (s)));
		}

		public override long GetItemId (int position)
		{
			return position;
		}

		public override bool HasStableIds {
			get {
				return false;
			}
		}

		public override View GetView (int position, View convertView, ViewGroup parent)
		{
			var view = convertView;
			if (view == null) {
				var inflater = context.GetSystemService (Context.LayoutInflaterService).JavaCast<LayoutInflater> ();
				view = inflater.Inflate (Resource.Layout.TwoItemsDropdown, parent, false);
			}

			var line1 = view.FindViewById<TextView> (Resource.Id.text1);
			var line2 = view.FindViewById<TextView> (Resource.Id.text2);

			var address = backingStore [position];
			var options1 = new string[] { address.FeatureName, address.Thoroughfare };
			var options2 = new string[] { address.Locality, address.AdminArea };

			line1.Text = string.Join (", ", options1.Where (s => !string.IsNullOrEmpty (s)).Distinct ());
			line2.Text = string.Join (", ", options2.Where (s => !string.IsNullOrEmpty (s)));

			return view;
		}

		public override int Count {
			get {
				return backingStore == null ? 0 : backingStore.Count;
			}
		}
	}
}

