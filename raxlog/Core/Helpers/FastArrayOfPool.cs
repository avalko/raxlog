using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Helpers
{
	internal static class FastArrayOfPool<T>
	{
		private static readonly Dictionary<int, Queue<T[]>> _cache = new Dictionary<int, Queue<T[]>>();

		public static T[] Rent(int length)
		{
			if (_cache.TryGetValue(length, out var value))
			{
				if (value.TryDequeue(out var result))
				{
					return result;
				}
			}
			else _cache[length] = new Queue<T[]>();

			return new T[length];
		}

		public static void Return(T[] array)
		{
			_cache[array.Length].Enqueue(array);
		}
	}
}
