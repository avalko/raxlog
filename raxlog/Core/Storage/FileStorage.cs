using RaxLog.Core.Abstract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RaxLog.Core.Storage
{
	internal sealed class FileStorage : AbstractStorage
	{
		private const string ERROR_FILE_DISPOSED = "Файл не был открыт или был выгружен!";
		private readonly string _workingDirectory;
		private FileStream _stream;
		private bool _opened = false;
		private string _resourceName;

		public override void Delete()
		{
			if (!_opened)
				throw new Exception(ERROR_FILE_DISPOSED);

			throw new NotImplementedException();
		}

		public override void Dispose()
		{
			if (!_opened)
				return;
			_opened = false;
			_stream.Dispose();
		}

		public override void Flush()
		{
			_stream.Flush();
		}


		private readonly string _filename;


		public FileStorage(string workingDirectory, string filename, IMetricsSource metricsSource)
			: base(metricsSource)
		{
			_filename = filename.ToUpper();
			_workingDirectory = workingDirectory;
			_resourceName = Path.Combine(_workingDirectory, _filename);
		}

		public override void Open()
		{
			if (_opened)
				throw new Exception("Файл уже открыт!");

			try
			{
				_stream = File.Open(_resourceName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
				_opened = true;
			}
			catch
			{
				throw new Exception($"Не удалось захватить ресурс: {_resourceName}");
			}
		}

		public override void Create()
		{
			if (_opened)
				throw new Exception("Файл уже открыт!");

			try
			{
				_stream = File.Open(_resourceName, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
				_opened = true;
			}
			catch
			{
				throw new Exception($"Не удалось захватить ресурс: {_resourceName}");
			}
		}

		public override void Read(Span<byte> buffer)
		{
			if (!_opened)
				throw new Exception(ERROR_FILE_DISPOSED);
			_stream.Read(buffer);
		}

		public override void Read(long offset, Span<byte> buffer)
		{
			if (!_opened)
				throw new Exception(ERROR_FILE_DISPOSED);
			_stream.Seek(offset, SeekOrigin.Begin);
			_stream.Read(buffer);
		}

		public override void SetPositionOffset(long offset)
		{
			if (!_opened)
				throw new Exception(ERROR_FILE_DISPOSED);
			_stream.Seek(offset, SeekOrigin.Begin);
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			if (!_opened)
				throw new Exception(ERROR_FILE_DISPOSED);
			_stream.Write(buffer);
		}

		public override void Write(long offset, ReadOnlySpan<byte> buffer)
		{
			if (!_opened)
				throw new Exception(ERROR_FILE_DISPOSED);
			_stream.Seek(offset, SeekOrigin.Begin);
			_stream.Write(buffer);
		}

		public override bool Exist()
		{
			return File.Exists(_resourceName);
		}
	}
}
