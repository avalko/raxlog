using RaxLog.Core.Abstract;
using RaxLog.Core.Storage;
using RaxLog.Core.Storage.Factories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RaxLog.Core
{
	public static class RaxManager
	{
		public static RaxLogTable OpenTable(string workingDirectory)
		{
			return new RaxLogTable(
				new FileStorageFactory(workingDirectory));
		}

		public static RaxLogTable OpenInMemoryTable(string tableName)
		{
			return new RaxLogTable(
				new MemoryStorageFactory(tableName));
		}

		public static IMetricsSource GetMetricsSource(string workingDirectory)
			=> MetricsManager.GetMetricsSource(new FileInfo(workingDirectory).FullName);
	}
}
