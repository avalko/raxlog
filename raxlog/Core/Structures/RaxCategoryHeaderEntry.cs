// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using System.Runtime.InteropServices;

namespace RaxLog.Core.Structures
{
	[StructLayout(LayoutKind.Explicit)]
	internal struct RaxCategoryHeaderEntry
	{
		[FieldOffset(0)]
		public long MinTimestamp;
		[FieldOffset(8)]
		public long MaxTimestamp;
		[FieldOffset(16)]
		public long LogsCountInCategory;
	}
}
