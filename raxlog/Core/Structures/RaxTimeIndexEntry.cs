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
