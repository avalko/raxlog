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
