// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using System.Runtime.InteropServices;

namespace RaxLog.Core.Structures
{
	/// <summary>
	/// Структура одной записи индекса по времени.
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct RaxTimeIndexEntry
	{
		[FieldOffset(0)]
		public long LogsCount;
		[FieldOffset(8)]
		public long FirstLogIndex;
	}
}
