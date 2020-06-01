using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RaxLog.Core.Abstract
{
	public abstract class AbstractStorage : IDisposable
	{
		protected readonly IMetricsSource _metricsSource;

		public virtual bool IsAppendOnly { get; protected set; } = false;
		public virtual bool IsReadOnly { get; protected set; } = false;

		public AbstractStorage(IMetricsSource metricsSource)
		{
			_metricsSource = metricsSource;
		}
		
		public IMetricsSource GetMetricsSource() => _metricsSource;

		public abstract void Delete();
		public abstract bool Exist();
		public abstract void Open();
		public abstract void Create();
		public abstract void SetPositionOffset(long offset);
		public abstract void Read(Span<byte> buffer);
		public abstract void Write(ReadOnlySpan<byte> buffer);
		public abstract void Read(long offset, Span<byte> buffer);
		public abstract void Write(long offset, ReadOnlySpan<byte> buffer);
		public abstract void Flush();
		public abstract void Dispose();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteStructure<T>(ref T resource)
			where T : unmanaged
		{
			var span = MemoryMarshal.CreateSpan(ref resource, 1);
			Write(MemoryMarshal.AsBytes(span));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteStructure<T>(long offset, ref T resource)
			where T : unmanaged
		{
			SetPositionOffset(offset);
			WriteStructure<T>(ref resource);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteStructureArray<T>(T[] inputResource)
			where T : unmanaged
		{
			var buffer = MemoryMarshal.CreateSpan(ref inputResource[0], inputResource.Length);
			var span = MemoryMarshal.AsBytes(buffer);
			Write(span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IMemoryOwner<T> ReadStructure<T>()
			where T : unmanaged
		{
			var buffer = MemoryPool<T>.Shared.Rent(1);
			var span = MemoryMarshal.AsBytes(buffer.Memory.Span);
			Read(span);
			return buffer;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ReadStructure<T>(ref T outputResource)
			where T : unmanaged
		{
			var buffer = MemoryMarshal.CreateSpan(ref outputResource, 1);
			var span = MemoryMarshal.AsBytes(buffer);
			Read(span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ReadStructureArray<T>(T[] outputResource)
			where T : unmanaged
		{
			var buffer = MemoryMarshal.CreateSpan(ref outputResource[0], outputResource.Length);
			var span = MemoryMarshal.AsBytes(buffer);
			Read(span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ReadStructureArray<T>(T[] outputResource, int outputOffset, int outputLength)
			where T : unmanaged
		{
			var buffer = MemoryMarshal.CreateSpan(ref outputResource[0], outputResource.Length)
				.Slice(outputOffset, outputLength);
			var span = MemoryMarshal.AsBytes(buffer);
			Read(span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IMemoryOwner<T> ReadStructure<T>(long offset)
			where T : unmanaged
		{
			SetPositionOffset(offset);
			return ReadStructure<T>();
		}
	}
}
