// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Helpers
{
	public static class FastArrayOfBytePool
	{
		private static readonly Dictionary<int, Queue<byte[]>> _cache = new Dictionary<int, Queue<byte[]>>();

		public static byte[] Rent(int length)
		{
			if (_cache.TryGetValue(length, out var value))
			{
				if (value.TryDequeue(out var result))
				{
					return result;
				}
			}
			else _cache[length] = new Queue<byte[]>();

			return new byte[length];
		}

		public static void Return(byte[] array)
		{
			_cache[array.Length].Enqueue(array);
		}
	}
}
