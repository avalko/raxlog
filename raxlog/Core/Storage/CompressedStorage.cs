// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using K4os.Compression.LZ4;
using RaxLog.Core.Abstract;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RaxLog.Core
{
	internal sealed class CompressedAppendStorage : AbstractStorage
	{
		private readonly AbstractStorage _appendOnlyDataStorage;
		private readonly AbstractStorage _compressedStorage;
		private readonly AbstractStorage _indexAndHeaderStorage;

		/// <summary>
		/// Приблизительный размер одного блока, после записи которого данные будут перенесены в архив.
		/// </summary>
		private const int ONE_BLOCK_SIZE_THRESHOLD = 2_000_000;

		/// <summary>
		/// Смещения указывающие где в сжатом файле храняться данные блока.
		/// </summary>
		private List<long> _blockOffsets;

		[StructLayout(LayoutKind.Explicit)]
		internal struct CompressedStorageHeader
		{
			[FieldOffset(0)]
			public int CompressedBlocksCount;

			[FieldOffset(4)]
			public long NextCompressedBlockOffseet;

			[FieldOffset(12)]
			public long TotalCompressedBytesCount;

			[FieldOffset(20)]
			public long TotalProcessedBytesCount;

			[FieldOffset(28)]
			public int RemainingBytesInUncompressedStorage;

			[FieldOffset(34)]
			public long MaxCompressedBufferSize;
		}

		private byte[] _reusableBuffer;
		private CompressedStorageHeader _header;
		private long _currentAbsoluteOffset;

		public CompressedAppendStorage(AbstractStorage appendStorage, AbstractStorage compressedStorage, AbstractStorage indexStorage,
			IMetricsSource metricsSource)
			: base(metricsSource)
		{
			_appendOnlyDataStorage = appendStorage;
			_compressedStorage = compressedStorage;
			_indexAndHeaderStorage = indexStorage;

			IsAppendOnly = true;
		}		

		public override bool Exist()
			=> _appendOnlyDataStorage.Exist();


		public override void Read(long offset, Span<byte> buffer)
		{
			SetPositionOffset(offset);
			Read(buffer);
		}

		public override void Write(long offset, ReadOnlySpan<byte> buffer)
		{
			SetPositionOffset(offset);
			Write(buffer);
		}

		public override void Create()
		{
			_indexAndHeaderStorage.Create();
			_appendOnlyDataStorage.Create();
			_compressedStorage.Create();

			_header = new CompressedStorageHeader
			{
				CompressedBlocksCount = 0,
				NextCompressedBlockOffseet = 0,
				TotalCompressedBytesCount = 0,
				TotalProcessedBytesCount = 0,
				RemainingBytesInUncompressedStorage = 0
			};
			_indexAndHeaderStorage.WriteStructure(0, ref _header);

			Initialize();
		}

		public override void Delete()
		{
			_indexAndHeaderStorage.Delete();
			_appendOnlyDataStorage.Delete();
			_compressedStorage.Delete();
		}

		public override void Dispose()
		{
			_indexAndHeaderStorage.Dispose();
			_appendOnlyDataStorage.Dispose();
			_compressedStorage.Dispose();
		}

		public override void Open()
		{
			_indexAndHeaderStorage.Open();
			_appendOnlyDataStorage.Open();
			_compressedStorage.Open();

			_indexAndHeaderStorage.SetPositionOffset(0);
			_indexAndHeaderStorage.ReadStructure(ref _header);

			Initialize();

			if (_header.CompressedBlocksCount > 0)
			{
				var offsetsBufferBytesCount = _header.CompressedBlocksCount * 8;
				var offsetsBuffer = new byte[offsetsBufferBytesCount];
				_indexAndHeaderStorage.ReadStructureArray(offsetsBuffer);

				var offsets = MemoryMarshal.Cast<byte, long>(offsetsBuffer);
				for (int i = 0; i < _header.CompressedBlocksCount; i++)
				{
					_blockOffsets.Add(offsets[i]);
				}
			}
		}

		private void Initialize()
		{
			_blockOffsets = new List<long>(_header.CompressedBlocksCount + 1000);
			_reusableBuffer = new byte[(int)(ONE_BLOCK_SIZE_THRESHOLD * 10)];
		}

		public override void SetPositionOffset(long offset)
		{
			_currentAbsoluteOffset = offset;
		}

		private unsafe void AppendNewCompressedBlock(byte[] compressedBytes, int compressedBytesCount, int originalBytesCount)
		{
			var offsetForCurrentCompressedBlock = _header.NextCompressedBlockOffseet;

			_indexAndHeaderStorage.SetPositionOffset(sizeof(CompressedStorageHeader) + _header.CompressedBlocksCount * 8);
			_indexAndHeaderStorage.WriteStructure(ref offsetForCurrentCompressedBlock);

			++_header.CompressedBlocksCount;
			_header.NextCompressedBlockOffseet += compressedBytesCount + 8; // COMPRESSED BYTES + COUNT(4) + COUNT ORIGINAL(4)
			_header.TotalCompressedBytesCount += compressedBytesCount;
			_header.TotalProcessedBytesCount += originalBytesCount;
			if (compressedBytesCount > _header.MaxCompressedBufferSize)
				_header.MaxCompressedBufferSize = compressedBytesCount;

			_indexAndHeaderStorage.SetPositionOffset(0);
			_indexAndHeaderStorage.WriteStructure(ref _header);

			_blockOffsets.Add(offsetForCurrentCompressedBlock);

			_compressedStorage.SetPositionOffset(offsetForCurrentCompressedBlock);
			ulong blockHeader = (uint)compressedBytesCount | ((ulong)originalBytesCount << 31);
			_compressedStorage.WriteStructure(ref blockHeader);
			_compressedStorage.Write(compressedBytes.AsSpan().Slice(0, compressedBytesCount));

			_metricsSource.SetMetricValue(MetricsConstants.COMPRESSED_BLOCKS_COUNT_I32, _header.CompressedBlocksCount);
			_metricsSource.SetMetricValue(MetricsConstants.TOTAL_COMPRESSED_BYTES_COUNT_I64, _header.TotalCompressedBytesCount);
			_metricsSource.SetMetricValue(MetricsConstants.TOTAL_PROCESSED_BYTES_COUNT_I64, _header.TotalProcessedBytesCount);
		}

		public override void Flush()
		{
			_appendOnlyDataStorage.Flush();
			_indexAndHeaderStorage.Flush();
			_compressedStorage.Flush();
		}

		public override void Read(Span<byte> buffer)
		{
			if (_header.CompressedBlocksCount == 0)
			{
				_appendOnlyDataStorage.SetPositionOffset(_currentAbsoluteOffset);
				_appendOnlyDataStorage.Read(buffer);
				return;
			}

			var absoluteOffset = _currentAbsoluteOffset;
			var blockIndex = 0;

			var reusableBufferSpan = _reusableBuffer.AsSpan();
			var compressedBuffer = new byte[_header.MaxCompressedBufferSize + 8];
			var fullCompressedBufferSpan = compressedBuffer.AsSpan();
			ref var headerOfCompressedBuffer = ref MemoryMarshal.Cast<byte, ulong>(fullCompressedBufferSpan.Slice(0, 8))[0];

			for (; ; )
			{
				if (_header.CompressedBlocksCount <= blockIndex || buffer.Length <= 0)
					break;

				var blockOffset = _blockOffsets[blockIndex++];
				_compressedStorage.SetPositionOffset(blockOffset);
				_compressedStorage.Read(compressedBuffer);

				var compressedDataLength = (int)(headerOfCompressedBuffer & 0xffffffff);
				var originalDataLength = (int)(headerOfCompressedBuffer >> 31);
				var localCompressedBufferSpan = fullCompressedBufferSpan.Slice(8, compressedDataLength);

				if (absoluteOffset >= originalDataLength)
				{
					absoluteOffset -= originalDataLength;
					continue;
				}

				if (_reusableBuffer.Length < originalDataLength)
				{
					Array.Resize(ref _reusableBuffer, (int)(originalDataLength * 1.5));
					reusableBufferSpan = _reusableBuffer.AsSpan();
				}

				LZ4Codec.Decode(localCompressedBufferSpan, reusableBufferSpan);

				var localOriginalBufferSpan = reusableBufferSpan.Slice(0, originalDataLength);

				if (absoluteOffset > 0)
				{
					localOriginalBufferSpan = localOriginalBufferSpan.Slice((int)absoluteOffset);
					originalDataLength -= (int)absoluteOffset;
					absoluteOffset = 0;
				}

				if (localOriginalBufferSpan.Length > buffer.Length)
				{
					localOriginalBufferSpan.Slice(0, buffer.Length).CopyTo(buffer);
					return;
				}

				localOriginalBufferSpan.CopyTo(buffer);
				buffer = buffer.Slice(originalDataLength);
			}

			if (buffer.Length > 0)
			{
				if (_header.RemainingBytesInUncompressedStorage < buffer.Length)
				{
					throw new Exception($"FATAL SYSTEM ERROR: {nameof(_header.RemainingBytesInUncompressedStorage)} < {nameof(buffer)}.{nameof(buffer.Length)}");
				}

				_appendOnlyDataStorage.SetPositionOffset(0);
				_appendOnlyDataStorage.Read(buffer);
			}
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			long relativeOffset = _currentAbsoluteOffset - _header.TotalProcessedBytesCount;
			long expectedSizeOfAppenOnlyDataStorage = relativeOffset + buffer.Length;

			if (expectedSizeOfAppenOnlyDataStorage > ONE_BLOCK_SIZE_THRESHOLD)
			{
				// Создаем буфер.
				var rawDataBuffer = new byte[expectedSizeOfAppenOnlyDataStorage];
				var rawSpan = rawDataBuffer.AsSpan();
				// Перемещаем в него все, что уже было записано в не сжатом виде.
				_appendOnlyDataStorage.SetPositionOffset(0);
				_appendOnlyDataStorage.Read(rawSpan.Slice(0, (int)relativeOffset));
				// ...и все, что пытаемся записать сейчас.
				buffer.CopyTo(rawSpan.Slice((int)relativeOffset));

				// Определяем максимальный размер данных после сжатия.
				var maxCompressedBufferSize = LZ4Codec.MaximumOutputSize((int)expectedSizeOfAppenOnlyDataStorage);
				// Выделяем под эти данные буфер.
				var compressedBuffer = new byte[maxCompressedBufferSize];
				// Сжимаем данные.
				var compressedBytesCount = LZ4Codec.Encode(rawDataBuffer, compressedBuffer, LZ4Level.L00_FAST);

				// Не сжатых данных больше нет.
				_header.RemainingBytesInUncompressedStorage = 0;
				// Сохраняем новый блок сжатых данных.
				AppendNewCompressedBlock(compressedBuffer, compressedBytesCount, (int)expectedSizeOfAppenOnlyDataStorage);

				_metricsSource.SetMetricValue(MetricsConstants.UNCOMPRESSED_BUFFER_SIZE_I32, 0);
			}
			else
			{
				_appendOnlyDataStorage.SetPositionOffset(relativeOffset);
				_appendOnlyDataStorage.Write(buffer);

				_header.RemainingBytesInUncompressedStorage = (int)expectedSizeOfAppenOnlyDataStorage;
				_indexAndHeaderStorage.SetPositionOffset(0);
				_indexAndHeaderStorage.WriteStructure(ref _header);

				_metricsSource.SetMetricValue(MetricsConstants.UNCOMPRESSED_BUFFER_SIZE_I32, _header.RemainingBytesInUncompressedStorage);
			}
		}
	}
}
