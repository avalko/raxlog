using RaxLog.Core.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RaxLog.Core.Storage.Factories
{
	internal sealed class MemoryStorageFactory : AbstractStorageFactory, IDisposable
	{
		private readonly BaseMetricsSource _metrics;
		private readonly string _tableName;

		public MemoryStorageFactory(string tableName)
		{
			_metrics = new BaseMetricsSource();
			_tableName = tableName;
			MetricsManager.RegisterMetricsSource(tableName, _metrics);
		}

		public override AbstractStorage CreateStorage(string filename, Parameters parameters)
		{
			if (parameters != null && parameters.AppendOnly)
			{
				var appendStorage = new MemoryStorage(_metrics);
				var compressedStorage = new MemoryStorage(_metrics);
				var compressedHeaderStorage = new MemoryStorage(_metrics);
				return new CompressedAppendStorage(appendStorage, compressedStorage, compressedHeaderStorage, _metrics);
			}
			return new MemoryStorage(_metrics);
		}

		public void Dispose()
		{
			MetricsManager.UnregisterMetricsSource(_tableName);
		}
	}
}
