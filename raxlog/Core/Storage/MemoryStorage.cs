using RaxLog.Core.Abstract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RaxLog.Core.Storage
{
	internal class MemoryStorage : AbstractStorage
	{
		private MemoryStream _stream;
		private bool _exists;

		public MemoryStorage(IMetricsSource metricsSource) : base(metricsSource)
		{
			_stream = new MemoryStream(100_000_000);
		}

		public override void Create()
		{
			_exists = true;
		}

		public override void Delete()
		{
		}

		public override void Dispose()
		{
			_stream.Dispose();
		}

		public override bool Exist()
			=> _exists;

		public override void Flush()
		{
		}

		public override void Open()
		{
			_exists = true;
		}

		public override void Read(Span<byte> buffer)
		{
			_stream.Read(buffer);
		}

		public override void Read(long offset, Span<byte> buffer)
		{
			_stream.Position = offset;
			_stream.Read(buffer);
		}

		public override void SetPositionOffset(long offset)
		{
			_stream.Position = offset;
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			_stream.Write(buffer);
		}

		public override void Write(long offset, ReadOnlySpan<byte> buffer)
		{
			_stream.Position = offset;
			_stream.Write(buffer);
		}
	}
}
