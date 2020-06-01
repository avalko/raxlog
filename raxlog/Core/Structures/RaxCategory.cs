using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Structures
{
	public readonly struct RaxCategory
	{
		public readonly string Name;
		public readonly long LogsCount;

		public RaxCategory(string name, long count)
		{
			Name = name;
			LogsCount = count;
		}
	}
}
