using System;

using Xamarin;

using Android.Content;

namespace Moyeu
{
	public class AnalyticsHelper
	{
		const string ApiKey = "...";

		static bool initialized;

		public static void Initialize (Context context)
		{
			/*if (!initialized) {
				try {
					Insights.Initialize (ApiKey, context);
				} catch {}
			}
			initialized = true;*/
		}

		public static void LogException (string tag, Exception e)
		{
			/*try {
				Insights.Report (e, "Tag", tag);
			} catch {}*/
		}
	}
}

