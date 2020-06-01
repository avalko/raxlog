using System.Runtime.InteropServices;

namespace RaxLog.Core.Structures
{
	/// <summary>
	/// Заголовок всей таблицы с логами.
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	internal struct RaxTableHeader
	{
		/// <summary>
		/// Всего логов в таблице.
		/// </summary>
		[FieldOffset(0)]
		public long TotalLogsCount;

		/// <summary>
		/// Метка первого лога.
		/// </summary>
		[FieldOffset(8)]
		public long MinTimestamp;

		/// <summary>
		/// Метка последнего лога.
		/// </summary>
		[FieldOffset(16)]
		public long MaxTimestamp;

		/// <summary>
		/// Количество категорий таблицы.
		/// </summary>
		[FieldOffset(24)]
		public long CategoriesCount;

		/// <summary>
		/// Указатель на первый свободный байт в хранилище данных логов.
		/// </summary>
		[FieldOffset(32)]
		public long LogsDataNextFreeOffset;
	}
}
