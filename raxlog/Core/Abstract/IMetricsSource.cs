// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

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
