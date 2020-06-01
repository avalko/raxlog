// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core
{
	public static class MetricsConstants
	{
		// По соображениям производительности встроенные в систему метрики имеют сокращенные имена.
		public const string UNCOMPRESSED_BUFFER_SIZE_I32		= "U_B_S_I32";
		public const string COMPRESSED_BLOCKS_COUNT_I32			= "C_B_C_I32";
		public const string TOTAL_COMPRESSED_BYTES_COUNT_I64	= "TC_B_C_I64";
		public const string TOTAL_PROCESSED_BYTES_COUNT_I64		= "TP_B_C_I64";
	}
}
