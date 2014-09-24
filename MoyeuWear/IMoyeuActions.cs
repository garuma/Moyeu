using System;

namespace Moyeu
{
	public interface IMoyeuActions
	{
		BikeActionStatus CurrentStatus { get; }
		void NavigateToStation (int stationID);
		void ToggleFavoriteStation (int stationID, bool cked);
	}
}

