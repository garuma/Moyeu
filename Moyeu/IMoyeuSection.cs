using System;

namespace Moyeu
{
	public interface IMoyeuSection
	{
		string Name { get; }
		string Title { get; }
		void RefreshData ();
		bool OnBackPressed ();
	}
}

