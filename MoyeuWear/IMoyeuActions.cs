using System;
using MoyeuWear;

namespace Moyeu
{
	public interface IMoyeuActions
	{
		BikeActionStatus CurrentStatus { get; }
		void NavigateToStation (SimpleStation station);
		void ToggleFavoriteStation (int stationID, bool cked);
	}
}

