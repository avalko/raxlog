using RaxLog.Core.Abstract;
using RaxLog.Core.Storage.Factories;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core
{
	public static class MetricsManager
	{
		private static ConcurrentDictionary<string, IMetricsSource> _sources;

		static MetricsManager()
		{
			_sources = new ConcurrentDictionary<string, IMetricsSource>();
		}

		internal static void RegisterMetricsSource(string name, IMetricsSource source)
		{
			_sources[name] = source;
		}

		public static IMetricsSource GetMetricsSource(string name)
			=> _sources.TryGetValue(name, out var source) ? source : null;

		internal static void UnregisterMetricsSource(string name)
		{
			_sources.TryRemove(name, out _);
		}
	}
}
