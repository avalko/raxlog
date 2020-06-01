using System.Runtime.InteropServices;

namespace RaxLog.Core.Structures
{
	/// <summary>
	/// Структура записи с информацией о логе.
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct RaxFixedSizeLogEntry
	{
		[FieldOffset(0)]
		public long Timestamp;
		[FieldOffset(8)]
		public long LogIndex;
		[FieldOffset(16)]
		public long DataOffset;
		[FieldOffset(24)]
		public int TextLengthInBytes;
		[FieldOffset(32)]
		public int CategoriesCount;
	}
}
