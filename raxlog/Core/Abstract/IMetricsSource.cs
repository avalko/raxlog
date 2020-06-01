using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Abstract
{
	public interface IMetricsSource
	{
		string[] GetMetricNames();

		IEnumerable<KeyValuePair<string, object>> GetAllMetrics();

		T GetMetricValue<T>(string key);

		internal void SetMetricValue<T>(string key, T value);
	}
}
