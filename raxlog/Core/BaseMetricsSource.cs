// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using RaxLog.Core.Abstract;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace RaxLog.Core
{
	internal sealed class BaseMetricsSource : IMetricsSource
	{
		private readonly Dictionary<string, object> _metricValues;
		private SpinLock _spin = new SpinLock();

		public BaseMetricsSource()
		{
			_metricValues = new Dictionary<string, object>();
		}

		public IEnumerable<KeyValuePair<string, object>> GetAllMetrics()
			=> _metricValues;

		string[] IMetricsSource.GetMetricNames() => _metricValues.Keys.ToArray();

		T IMetricsSource.GetMetricValue<T>(string key)
		{
			var lockTaken = false;
			_spin.Enter(ref lockTaken);
			_metricValues.TryGetValue(key, out var obj);
			if (lockTaken) _spin.Exit();
			if (obj is T value)
			{
				return value;
			}
			return default;
		}

		void IMetricsSource.SetMetricValue<T>(string key, T value)
		{
			var lockTaken = false;
			_spin.Enter(ref lockTaken);
			_metricValues[key] = value;
			if (lockTaken) _spin.Exit();
		}
	}
}
