// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

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
