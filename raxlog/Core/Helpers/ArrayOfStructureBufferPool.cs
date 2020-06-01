// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Helpers
{
	internal unsafe static class ArrayOfStructureBufferPool<T>
		where T : unmanaged
	{
		private static int _sizeOf = sizeof(T);
		private static Queue<byte[]> _cache = new Queue<byte[]>();

		static ArrayOfStructureBufferPool()
		{
			for (int i = 0; i < 100; i++)
			{
				_cache.Enqueue(new byte[_sizeOf]);
			}
		}

		public static byte[] Rent()
		{
			if (_cache.TryDequeue(out var result))
				return result;
			return new byte[_sizeOf];
		}

		public static void Return(byte[] buffer)
		{
			_cache.Enqueue(buffer);
		}
	}
}
