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
