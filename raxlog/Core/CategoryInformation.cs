// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using RaxLog.Core.Structures;

namespace RaxLog.Core
{
	internal class CategoryInformation
	{
		public long CategoryIndex;
		public string CategoryName;
		public long MinTimestamp;
		public long MaxTimestamp;
		public long LogsCount;

		public bool IsNewCategory;

		public void ProcessNewLogInCategory(RaxNewLog log)
		{
			if (LogsCount++ == 0)
			{
				MinTimestamp = MaxTimestamp = log.Timestamp;
			}
			else
			{
				if (log.Timestamp > MaxTimestamp)
					MaxTimestamp = log.Timestamp;
			}
		}
	}
}
