// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using RaxLog.Core.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RaxLog.Core.Storage.Factories
{
	internal sealed class FileStorageFactory : AbstractStorageFactory, IDisposable
	{
		private readonly string _workingDirectory;
		private readonly BaseMetricsSource _metrics;

		public FileStorageFactory(string workingDirectory)
		{
			_metrics = new BaseMetricsSource();
			MetricsManager.RegisterMetricsSource(new FileInfo(workingDirectory).FullName, _metrics);

			_workingDirectory = workingDirectory;
			if (!Directory.Exists(workingDirectory))
				Directory.CreateDirectory(workingDirectory);
		}

		public override AbstractStorage CreateStorage(string filename, Parameters parameters)
		{
			if (parameters != null && parameters.AppendOnly)
			{
				var appendStorage = new FileStorage(_workingDirectory, filename, _metrics);
				var compressedStorage = new FileStorage(_workingDirectory, filename + ".LZ4", _metrics);
				var compressedHeaderStorage = new FileStorage(_workingDirectory, filename + ".LZ4-HEAD", _metrics);
				return new CompressedAppendStorage(appendStorage, compressedStorage, compressedHeaderStorage, _metrics);
			}
			return new FileStorage(_workingDirectory, filename, _metrics);
		}

		public void Dispose()
		{
			MetricsManager.UnregisterMetricsSource(new FileInfo(_workingDirectory).FullName);
		}
	}
}
