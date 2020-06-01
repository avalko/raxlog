// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using System;
using System.Collections.Generic;
using System.Text;

namespace RaxLog.Core.Abstract
{
	public abstract class AbstractStorageFactory
	{
		public class Parameters
		{
			public bool AppendOnly { get; set; }

			public readonly static Parameters AppendOnlyStorage = new Parameters { AppendOnly = true };
		}

		public abstract AbstractStorage CreateStorage(string storageName, Parameters parameters = null);
	}
}
