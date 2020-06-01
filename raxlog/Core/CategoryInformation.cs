using RaxLog.Core.Structures;

namespace RaxLog.Core
{
	internal class CategoryInformation
	{
		public long CategoryIndex;
		public string CategoryName;
		public long MinTimestamp;
		public long MaxTimestamp;
		public long LogsCount;

		public bool IsNewCategory;

		public void ProcessNewLogInCategory(RaxNewLog log)
		{
			if (LogsCount++ == 0)
			{
				MinTimestamp = MaxTimestamp = log.Timestamp;
			}
			else
			{
				if (log.Timestamp > MaxTimestamp)
					MaxTimestamp = log.Timestamp;
			}
		}
	}
}
