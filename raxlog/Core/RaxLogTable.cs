// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using K4os.Compression.LZ4;
using RaxLog.Core.Abstract;
using RaxLog.Core.Helpers;
using RaxLog.Core.Structures;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RaxLog.Core
{
	public unsafe class RaxLogTable : IDisposable
	{
		/// <summary>
		/// Файл с заголовком таблицы, её описанием.
		/// </summary>
		public const string FILE_TABLE_BIN = "_table.bin";
		/// <summary>
		/// Файл с логами без текста и категорий и ссылкам на смещение в файле данных дял каждого лога.
		/// </summary>
		public const string FILE_LOGS = "_logs.bin";
		/// <summary>
		/// Файл данных с текстом и категориями каждого лога.
		/// </summary>
		public const string FILE_LOGS_DATA = "_logs_data.bin";
		/// <summary>
		/// Файл со списком всех категорий.
		/// </summary>
		public const string FILE_CATEGORIES = "_categories.bin";
		/// <summary>
		/// Файл с индексом по каждой минуте в этом году.
		/// </summary>
		public const string FILE_INDEX_YEAR_YYYY = "_index_by_year_{0}_minutes.bin";

		private readonly AbstractStorageFactory _factory;
		private readonly AbstractStorage _tableHeaderStorage;
		private readonly AbstractStorage _logsStorage;
		private readonly AbstractStorage _logsDataStorage;
		private readonly AbstractStorage _categoriesStorage;

		private RaxTableHeader _tableHeaderStructure;
		private readonly Dictionary<int, RaxTimeIndexEntry[]> _yearMinutesIndexCache;
		private readonly Dictionary<int, AbstractStorage> _indexYearStorages;
		private readonly Dictionary<string, CategoryInformation> _categoriesByName;
		private readonly Dictionary<long, CategoryInformation> _categoriesByIndex;

		private readonly static int _logSize = sizeof(RaxFixedSizeLogEntry);
		private readonly static int _catSize = sizeof(RaxCategoryEntry);
		private readonly static int _catShortSize = sizeof(RaxCategoryHeaderEntry);
		private readonly static int _logIndexSize = sizeof(RaxTimeIndexEntry);

		public long MinTimestamp => _tableHeaderStructure.MinTimestamp;
		public long MaxTimestamp => _tableHeaderStructure.MaxTimestamp;
		public long TotalLogs => _tableHeaderStructure.TotalLogsCount;
		public RaxCategory[] Categories => _categoriesByName.Values.Select(x => new RaxCategory(x.CategoryName, x.LogsCount)).ToArray();

		public RaxLogTable(AbstractStorageFactory factory)
		{
			_factory = factory;

			_tableHeaderStorage = factory.CreateStorage(FILE_TABLE_BIN);
			_logsStorage = factory.CreateStorage(FILE_LOGS);
			_categoriesStorage = factory.CreateStorage(FILE_CATEGORIES);
			_logsDataStorage = factory.CreateStorage(FILE_LOGS_DATA, AbstractStorageFactory.Parameters.AppendOnlyStorage);

			_categoriesByName = new Dictionary<string, CategoryInformation>();
			_categoriesByIndex = new Dictionary<long, CategoryInformation>();
			_indexYearStorages = new Dictionary<int, AbstractStorage>();
			_yearMinutesIndexCache = new Dictionary<int, RaxTimeIndexEntry[]>();

			if (_tableHeaderStorage.Exist())
			{
				OpenExistingTable();
			}
			else
			{
				CreateNewTable();
			}
		}

		private void OpenExistingTable()
		{
			// Attempt to read table data.
			_tableHeaderStorage.Open();
			_logsStorage.Open();
			_logsDataStorage.Open();
			_categoriesStorage.Open();

			_tableHeaderStorage.ReadStructure(ref _tableHeaderStructure);

			// Load categories.
			var oneCategoryEntry = default(RaxCategoryEntry);
			for (int i = 0; i < _tableHeaderStructure.CategoriesCount; i++)
			{
				var category = new CategoryInformation();

				_categoriesStorage.SetPositionOffset(i * _catSize);
				_categoriesStorage.ReadStructure(ref oneCategoryEntry);

				category.CategoryIndex = i;
				category.CategoryName = new string(oneCategoryEntry.CategoryName, 0, oneCategoryEntry.CategoryRealNameLengthInBytes / 2);
				category.LogsCount = oneCategoryEntry.Header.LogsCountInCategory;
				category.MinTimestamp = oneCategoryEntry.Header.MinTimestamp;
				category.MaxTimestamp = oneCategoryEntry.Header.MaxTimestamp;

				_categoriesByIndex[category.CategoryIndex] = category;
				_categoriesByName[category.CategoryName] = category;
			}

			// Load indexes
			for (int year = new DateTime(_tableHeaderStructure.MinTimestamp).Year;
				year <= new DateTime(_tableHeaderStructure.MaxTimestamp).Year;
				++year)
			{
				GetOrCreateYearIndexStorage(year);
			}
		}

		private unsafe void CreateNewTable()
		{
			// Initialize table.
			_tableHeaderStorage.Create();
			_logsStorage.Create();
			_logsDataStorage.Create();
			_categoriesStorage.Create();

			var datetime = DateTime.UtcNow;

			var now = datetime.ToTimestamp();
			_tableHeaderStructure = new RaxTableHeader
			{
				MinTimestamp = now,
				MaxTimestamp = now,
				TotalLogsCount = 0,
				CategoriesCount = 0,
				LogsDataNextFreeOffset = 0
			};

			GetOrCreateYearIndexStorage(datetime.Year);
			_tableHeaderStorage.WriteStructure(ref _tableHeaderStructure);

			CreateInitialCategories();
			void CreateInitialCategories()
			{
				var categoryBuffer = ArrayPool<byte>.Shared.Rent(_catSize);
				WriteCategoryToStorage(categoryBuffer, CreateCategory("TRACE"));
				WriteCategoryToStorage(categoryBuffer, CreateCategory("DEBUG"));
				WriteCategoryToStorage(categoryBuffer, CreateCategory("INFO"));
				WriteCategoryToStorage(categoryBuffer, CreateCategory("WARNING"));
				WriteCategoryToStorage(categoryBuffer, CreateCategory("ERROR"));
				WriteCategoryToStorage(categoryBuffer, CreateCategory("CRITICAL"));
				ArrayPool<byte>.Shared.Return(categoryBuffer);
			}

			AppendLogs(new RaxNewLog[]
			{
				new RaxNewLog
				{
					Message = "SYSTEM READY.".ToCharArray(),
					Categories = new[] { "@SYS_INFO" },
					Timestamp = now
				}
			});
		}

		private RaxTimeIndexEntry[] GetOrCreateYearMinutesIndexCache(int year)
		{
			if (!_yearMinutesIndexCache.TryGetValue(year, out var cache))
			{
				long lastMinuteInYear = new DateTime(year, 12, 31, 23, 59, 59).MinutesFromYearBegin();
				cache = new RaxTimeIndexEntry[lastMinuteInYear + 1];
				_yearMinutesIndexCache[year] = cache;
			}
			return cache;
		}

		private AbstractStorage GetOrCreateYearIndexStorage(int year)
		{
			if (!_indexYearStorages.TryGetValue(year, out var storage))
			{
				storage = _factory.CreateStorage(string.Format(FILE_INDEX_YEAR_YYYY, year));

				var yearMinutes = GetOrCreateYearMinutesIndexCache(year);

				if (storage.Exist())
				{
					storage.Open();
					storage.SetPositionOffset(0);
					storage.ReadStructureArray(yearMinutes);
				}
				else
				{
					// Если такого хранилища нет, то создаем его и записываем на диск (редкое явление).
					storage.Create();
					storage.SetPositionOffset(0);
					storage.WriteStructureArray(yearMinutes);
					storage.Flush();
				}

				_indexYearStorages[year] = storage;
			}

			return storage;
		}

		private void CreateOrUpdateYearIndexStorage(int year)
		{
			if (!_indexYearStorages.TryGetValue(year, out var storage))
			{
				storage = _factory.CreateStorage(string.Format(FILE_INDEX_YEAR_YYYY, year));

				_indexYearStorages[year] = storage;

				storage.Create();
			}

			var yearMinutes = GetOrCreateYearMinutesIndexCache(year);

			storage.SetPositionOffset(0);
			storage.WriteStructureArray(yearMinutes);
			storage.Flush();
		}

		private CategoryInformation CreateCategory(string categoryName)
		{
			var nextCategoryIndex = _tableHeaderStructure.CategoriesCount++;

			var newCategory = new CategoryInformation
			{
				CategoryIndex = nextCategoryIndex,
				CategoryName = categoryName,
				LogsCount = 0,
				IsNewCategory = true
			};

			_categoriesByName[categoryName] = newCategory;
			_categoriesByIndex[nextCategoryIndex] = newCategory;

			return newCategory;
		}

		private readonly Dictionary<int, CategoryInformation[]> _cacheCategoryArrays = new Dictionary<int, CategoryInformation[]>()
		{
			[1] = new CategoryInformation[1],
			[2] = new CategoryInformation[2],
			[3] = new CategoryInformation[3],
		};

		/// <summary>
		/// Преобразует имена категорий в массив объектов.
		/// </summary>
		/// <param name="categories">Список имен категорий.</param>
		private CategoryInformation[] MapCategoryNamesToCategoryInstances(string[] categories)
		{
			// В ходе тестирования было выявлено, что использование тут словаря
			// с кешированными массивами повышает производительность метода.
			var result = _cacheCategoryArrays.TryGetValue(categories.Length, out var cache)
				? cache
				: (_cacheCategoryArrays[categories.Length] = new CategoryInformation[categories.Length]);

			int index = 0;
			foreach (var category in categories)
			{
				result[index++] =
					_categoriesByName.TryGetValue(category, out var catInfo)
					? catInfo
					: CreateCategory(category);
			}

			return result;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int ComputeLogDataSizeInBytes(int logTextLengthInChars, int logCategoriesCount)
		{
			return (logTextLengthInChars * 2) + (8 * logCategoriesCount);
		}

		public RaxLogEntry[] GetLogs(long minTimestamp, long maxTimestamp, long skipLogs = 0, long takeLogs = long.MaxValue)
		{
			var range = GetLogsRange(minTimestamp, maxTimestamp, skipLogs, takeLogs);

			var result = new RaxLogEntry[range.LogsCount];

			if (range.LogsCount == 0)
			{
				return result;
			}

			// MAX_SELECT_LOGS_BUFFER
			var bufferSize = range.LogsCount * _logSize;

			var buffer = new byte[bufferSize];
			_logsStorage.SetPositionOffset(range.FromLogIndex * _logSize);
			_logsStorage.ReadStructureArray(buffer);

			var logs = MemoryMarshal.Cast<byte, RaxFixedSizeLogEntry>(buffer);

			var fromLog = logs[0];
			var toLog = logs[logs.Length - 1];

			var fromOffset = fromLog.DataOffset;
			var toOffset = toLog.DataOffset;
			var logsBytesData = toOffset + toLog.TextLengthInBytes + toLog.CategoriesCount * 8;

			// MAX_SELECT_LOGS_BUFFER
			var dataBuffer = new byte[logsBytesData];
			_logsDataStorage.SetPositionOffset(fromOffset);
			_logsDataStorage.ReadStructureArray(dataBuffer);

			var dataBufferSpan = dataBuffer.AsSpan();

			var dataOffset = 0;
			for (int i = 0; i < logs.Length; i++)
			{
				var log = logs[i];

				var dataSpanBegin = dataBufferSpan.Slice(dataOffset);
				var text = new string(MemoryMarshal.Cast<byte, char>(dataSpanBegin.Slice(0, log.TextLengthInBytes)));
				var datetime = new DateTime(log.Timestamp);

				var categories = new string[log.CategoriesCount];

				var logCategoriesSpan = MemoryMarshal.Cast<byte, long>(dataSpanBegin.Slice(log.TextLengthInBytes, log.CategoriesCount * 8));
				for (int catIndex = 0; catIndex < log.CategoriesCount; catIndex++)
				{
					categories[catIndex] = _categoriesByIndex[logCategoriesSpan[catIndex]].CategoryName;
				}

				result[i] = new RaxLogEntry
				{
					Text = text,
					Date = datetime,
					Categories = categories
				};

				dataOffset += ComputeLogDataSizeInBytes(text.Length, log.CategoriesCount);
			}

			return result;
		}

		internal struct LogsRange
		{
			public long LogsCount;
			public long FromLogIndex;
		}

		internal LogsRange GetLogsRange(long minTimestamp, long maxTimestamp, long skipLogs = 0, long takeLogs = long.MaxValue)
		{
			var takeRemainingLogs = takeLogs;
			var range = default(LogsRange);

			var fromDatetime = new DateTime(minTimestamp);
			var toDatetime = new DateTime(maxTimestamp);

			var fromYear = fromDatetime.Year;
			var toYear = toDatetime.Year;

			var fromFirstYearMinute = fromDatetime.MinutesFromYearBegin();
			var toLastYearMinute = toDatetime.MinutesFromYearBegin();

			var yearsDiff = toYear - fromYear;

			for (int yearIndex = 0; yearIndex <= yearsDiff; yearIndex++)
			{
				var year = fromYear + yearIndex;

				if (_yearMinutesIndexCache.TryGetValue(year, out var indexCache))
				{
					var lastMinuteInYear = indexCache.Length - 1;
					var fromMinute = (yearsDiff == 0 || yearIndex == 0) ? fromFirstYearMinute : 0;
					var toMinute = (yearsDiff == 0 || yearIndex == yearsDiff) ? toLastYearMinute : lastMinuteInYear;

					for (long minute = fromMinute; minute <= toMinute; minute++)
					{
						ref var cacheEntry = ref indexCache[minute];

						var logsCountPerMinute = cacheEntry.LogsCount;

						// Количество логов за эту минуту.
						if (logsCountPerMinute > 0)
						{
							var firstLogIndexInMinute = cacheEntry.FirstLogIndex;

							// Если нужно пропустить часть логов.
							if (skipLogs > 0)
							{
								// Если нужно пропустить больше, чем есть логов в этой минуте.
								if (skipLogs >= logsCountPerMinute)
								{
									// Просто пропускаем её.
									skipLogs -= logsCountPerMinute;
									continue;
								}
								// Если в этой минуте больше логов, чем нужно пропустить.
								logsCountPerMinute -= skipLogs;
								// Смещаем индекс первого лога минуты.
								firstLogIndexInMinute += skipLogs;
								skipLogs = 0; // Больше пропускать не нужно.
							}

							// Если в диапазоне ещё нет ни одного лога, то фиксируем начало диапазона.
							if (range.LogsCount == 0)
							{
								range.FromLogIndex = firstLogIndexInMinute;
							}
							// Добавляем в диапазон логи этой минуты.
							range.LogsCount += logsCountPerMinute;

							// Если ещё не достигли установленного предела в количестве логов.
							if (takeRemainingLogs > 0)
							{
								// Если только что мы добавили в диапазон столько логов, сколько нам нужно взять всего.
								if (takeRemainingLogs == logsCountPerMinute)
									return range; // Возвращаем диапазон.

								// Если мы только что добавили в диапазон больше логов, чем нам нужно взять.
								if (logsCountPerMinute > takeRemainingLogs)
								{
									var diff = logsCountPerMinute - takeRemainingLogs;
									// Уменьшаем диапазон на число, которые мы превысили.
									range.LogsCount -= diff;
									return range;  // Возвращаем диапазон.
								}

								// Если мы ещё не заполнили диапазон необходимым количеством логов, продолжаем.
								takeRemainingLogs -= logsCountPerMinute;
							}
							else return range;
						}
					}
				}
			}

			return range;
		}

		private readonly HashSet<CategoryInformation> _cacheCategoriesHashSet = new HashSet<CategoryInformation>();

		public void AppendLogs(Span<RaxNewLog> logs)
		{
			var newLogsCount = logs.Length;
			int totalLogBytes = newLogsCount * _logSize;
			int totalLogDataBytes = 0;

			var firstLogIndex = _tableHeaderStructure.TotalLogsCount;
			var firstLogDataOffset = _tableHeaderStructure.LogsDataNextFreeOffset;

			// Считаем размер всех логов, обновляем категории.
			_cacheCategoriesHashSet.Clear();
			for (int i = 0; i < newLogsCount; i++)
			{
				ref var log = ref logs[i];
				log._computedDataSize = ComputeLogDataSizeInBytes(log.Message.Length, log.Categories.Length);
				totalLogDataBytes += log._computedDataSize;

				log._computedCategoryIndexes = FastArrayOfLongPool.Rent(log.Categories.Length);

				int localCatIndex = 0;
				foreach (var category in MapCategoryNamesToCategoryInstances(log.Categories))
				{
					_cacheCategoriesHashSet.Add(category);
					category.ProcessNewLogInCategory(log);
					log._computedCategoryIndexes[localCatIndex++] = category.CategoryIndex;
				}
			}

			// Записываем обновленные категории на диск.
			var categoryBuffer = ArrayOfStructureBufferPool<RaxCategoryEntry>.Rent();
			foreach (var category in _cacheCategoriesHashSet)
			{
				WriteCategoryToStorage(categoryBuffer, category);
			}
			ArrayOfStructureBufferPool<RaxCategoryEntry>.Return(categoryBuffer);
			_categoriesStorage.Flush();

			// Новая временная метка последнего лога.
			var newMaxTimestamp = logs[newLogsCount - 1].Timestamp;
			// Буферы для логов.
			var sharedBufferLogs = FastArrayOfBytePool.Rent(totalLogBytes);
			var sharedBufferData = FastArrayOfBytePool.Rent(totalLogDataBytes);
			var sharedBufferLogsSpan = MemoryMarshal.Cast<byte, RaxFixedSizeLogEntry>(sharedBufferLogs);

			// Обновляем данные.
			var skippedLogDataBytesCount = 0;
			for (int i = 0; i < newLogsCount; i++)
			{
				ref var log = ref logs[i];

				// LOG
				sharedBufferLogsSpan[i] = new RaxFixedSizeLogEntry
				{
					LogIndex = firstLogIndex + i,
					DataOffset = firstLogDataOffset + skippedLogDataBytesCount,
					TextLengthInBytes = log.Message.Length * 2,
					Timestamp = log.Timestamp,
					CategoriesCount = log.Categories.Length
				};

				// LOG DATA
				Buffer.BlockCopy(log.Message, 0, sharedBufferData, skippedLogDataBytesCount, log.Message.Length * 2);
				Buffer.BlockCopy(log._computedCategoryIndexes, 0,
					sharedBufferData, skippedLogDataBytesCount + log.Message.Length * 2, log.Categories.Length * 8);

				skippedLogDataBytesCount += log._computedDataSize;

				FastArrayOfLongPool.Return(log.Categories.Length, log._computedCategoryIndexes);
			}

			// Записываем информацию о логе.
			_logsStorage.SetPositionOffset(firstLogIndex * _logSize);
			_logsStorage.Write(sharedBufferLogs);
			// Записываем данные - текст лога и его категории.
			_logsDataStorage.SetPositionOffset(firstLogDataOffset);
			_logsDataStorage.Write(sharedBufferData.AsSpan().Slice(0, totalLogDataBytes));

			FastArrayOfBytePool.Return(sharedBufferLogs);
			FastArrayOfBytePool.Return(sharedBufferData);

			_logsStorage.Flush();
			_logsDataStorage.Flush();

			// Обновляем индексы.
			var yearBegin = new DateTime(_tableHeaderStructure.MaxTimestamp).Year;
			var yearsCount = new DateTime(newMaxTimestamp).Year - yearBegin;
			var localYearMinutes = FastArrayOfPool<bool>.Rent(yearsCount + 1);
			for (int localLogIndex = 0; localLogIndex < newLogsCount; localLogIndex++)
			{
				ref var log = ref logs[localLogIndex];
				var logDatetime = new DateTime(log.Timestamp);
				var logYear = logDatetime.Year;

				var localYearArrayIndex = logYear - yearBegin;
				localYearMinutes[localYearArrayIndex] = true;

				var cachedYearMinutes = GetOrCreateYearMinutesIndexCache(logYear);
				ref var cachedMinute = ref cachedYearMinutes[logDatetime.MinutesFromYearBegin()];

				if (cachedMinute.LogsCount == 0)
				{
					// В начале задаем значение лога, и только потом увеличиваем счетчик.
					// Если чтение будет происходить параллельно, это не даст прочитать от сюда нулевое/не установленное значение.
					cachedMinute.FirstLogIndex = firstLogIndex + localLogIndex;
				}

				++cachedMinute.LogsCount;
			}

			// Записываем индексы на диск.
			for (int yearIndex = 0; yearIndex <= yearsCount; yearIndex++)
			{
				if (localYearMinutes[yearIndex])
				{
					var year = yearIndex + yearBegin;
					CreateOrUpdateYearIndexStorage(year);
				}
			}
			FastArrayOfPool<bool>.Return(localYearMinutes);

			// Обновляем заголовок таблицы.
			UpdateTableHeader(
				addedLogsCount: newLogsCount,
				addedDataBytes: totalLogDataBytes,
				newMaxTimestamp);
		}

		/// <summary>
		/// Записывает категорию лога в хранилище.
		/// </summary>
		/// <param name="tempBuffer">Временный буффер используемый для записи категории.</param>
		/// <param name="category">Категория.</param>
		private void WriteCategoryToStorage(byte[] tempBuffer, CategoryInformation category)
		{
			var tempBufferSpan = tempBuffer.AsSpan();
			var catNameTextLengthInBytes = category.CategoryName.Length * 2;

			var categoryHeader = new RaxCategoryHeaderEntry
			{
				MinTimestamp = category.MinTimestamp,
				MaxTimestamp = category.MaxTimestamp,
				LogsCountInCategory = category.LogsCount
			};
			new Span<byte>((byte*)&categoryHeader, _catShortSize)
				.CopyTo(tempBufferSpan);

			_categoriesStorage.SetPositionOffset(category.CategoryIndex * _catSize);

			if (category.IsNewCategory)
			{
				new Span<byte>((byte*)&catNameTextLengthInBytes, 4)
					.CopyTo(tempBufferSpan.Slice(_catShortSize));

				Buffer.BlockCopy(category.CategoryName.ToCharArray(), 0,
					tempBuffer, sizeof(RaxCategoryHeaderEntry) + /* CAT_NAME_LENGTH(4 BYTE) */ 4,
					catNameTextLengthInBytes);

				category.IsNewCategory = false;

				_categoriesStorage.Write(tempBufferSpan.Slice(0, _catSize));
			}
			else
			{
				_categoriesStorage.Write(tempBufferSpan.Slice(0, sizeof(RaxCategoryHeaderEntry)));
			}
		}

		/// <summary>
		/// Обновляет заголовок таблицы логов.
		/// </summary>
		/// <param name="addedLogsCount">Сколько логов было добавлено в таблицу.</param>
		/// <param name="addedDataBytes">Сколько байт данных логов было добавлено в таблицу.</param>
		/// <param name="maxTimestamp">Метка времени последнего добавленного лога.</param>
		private void UpdateTableHeader(int addedLogsCount, long addedDataBytes, long maxTimestamp)
		{
			_tableHeaderStructure.TotalLogsCount += addedLogsCount;
			_tableHeaderStructure.LogsDataNextFreeOffset += addedDataBytes;
			if (_tableHeaderStructure.MaxTimestamp < maxTimestamp)
				_tableHeaderStructure.MaxTimestamp = maxTimestamp;
			_tableHeaderStorage.WriteStructure(0, ref _tableHeaderStructure);
			_tableHeaderStorage.Flush();
		}

		public void Dispose()
		{
			_categoriesStorage.Dispose();
			_logsDataStorage.Dispose();
			_logsDataStorage.Dispose();
			_logsStorage.Dispose();
			_tableHeaderStorage.Dispose();
			foreach (var storage in _indexYearStorages)
			{
				storage.Value.Dispose();
			}
		}
	}
}
