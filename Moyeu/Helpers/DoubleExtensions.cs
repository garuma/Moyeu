using System;

namespace Moyeu
{
	public static class DoubleExtensions
	{
		static readonly IFormatProvider neutralProvider =
			System.Globalization.CultureInfo.InvariantCulture;

		public static double ToSafeDouble (this string input)
		{
			if (string.IsNullOrEmpty (input))
				return 0d;
			return double.Parse (input, neutralProvider);
		}

		public static string ToSafeString (this double input)
		{
			return input.ToString (neutralProvider);
		}
	}
}

