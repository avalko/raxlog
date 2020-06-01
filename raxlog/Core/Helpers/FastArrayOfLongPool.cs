// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Helpers
{
	internal static class FastArrayOfLongPool
	{
		private static readonly Dictionary<int, Queue<long[]>> _cache = new Dictionary<int, Queue<long[]>>();

		static FastArrayOfLongPool()
		{
			for (int arrSize = 0; arrSize < 10; arrSize++)
			{
				var queue = _cache[arrSize] = new Queue<long[]>();
				for (int i = 0; i < 100_000; i++)
				{
					queue.Enqueue(new long[arrSize]);
				}
			}
		}

		public static long[] Rent(int length)
		{
			if (_cache.TryGetValue(length, out var value))
			{
				if (value.TryDequeue(out var result))
				{
					return result;
				}
			}
			else _cache[length] = new Queue<long[]>();

			return new long[length];
		}

		public static void Return(int length, long[] array)
		{
			_cache[length].Enqueue(array);
		}
	}
}
