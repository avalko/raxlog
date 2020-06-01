using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Structures
{
	public struct RaxNewLog
	{
		public char[] Message;
		public string[] Categories;
		public long Timestamp;

		internal int _computedDataSize;
		internal long[] _computedCategoryIndexes;
	}
}
