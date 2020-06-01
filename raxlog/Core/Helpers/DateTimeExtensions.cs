using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace RaxLog.Core.Helpers
{
	public static class DateTimeExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ToTimestamp(this DateTime dateTime)
			=> dateTime.Ticks;

		public static long MinutesFromYearBegin(this DateTime dateTime)
		{
			return dateTime.DayOfYear * 24 * 60 + dateTime.Hour * 60 + dateTime.Minute;
		}
	}
}
