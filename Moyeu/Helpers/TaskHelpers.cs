using System;
using System.Threading.Tasks;
using Android.Util;

namespace Moyeu
{
	public static class TaskHelpers
	{
		public static async void IgnoreIfFaulted (this Task original, string logMessage = null)
		{
			try {
				if (original == null)
					return;
				await original.ConfigureAwait (false);
			} catch (Exception e) {
				if (logMessage != null)
					Log.Error ("AsyncError", logMessage + " " + e.ToString ());
			}
		}

		public static async Task PreventExceptionThrowing (this Task original)
		{
			try {
				await original;
			} catch { }
		}

		public static async Task<T> DefaultIfNullOrCanceled<T> (this Task<T> original, T defaultValue = default (T))
		{
			if (original == null)
				return defaultValue;
			try {
				return await original.ConfigureAwait (false);
			} catch (OperationCanceledException) {
				return defaultValue;
			}
		}

		public static async Task<T> DefaultIfFaulted<T> (this Task<T> original, T defaultValue, string logMessage = null)
		{
			try {
				return await original.ConfigureAwait (false);
			} catch (Exception e) {
				if (logMessage != null)
					Log.Error ("AsyncError", logMessage + " " + e.ToString ());
				return defaultValue;
			}
		}
	}
}
