using System.Runtime.InteropServices;

namespace RaxLog.Core.Structures
{
	/// <summary>
	/// Струтктура записи с инфорамацией о категории.
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal unsafe struct RaxCategoryEntry
	{
		[FieldOffset(0)]
		public RaxCategoryHeaderEntry Header;
		[FieldOffset(24)]
		public int CategoryRealNameLengthInBytes;
		[FieldOffset(28)]
		public fixed char CategoryName[512];
	}
}
