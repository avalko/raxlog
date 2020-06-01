// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

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
