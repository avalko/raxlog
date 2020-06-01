using BenchmarkDotNet.Attributes;
using RaxLog.Core;
using RaxLog.Core.Helpers;
using RaxLog.Core.Structures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace RaxLog.Tests
{
	public class LogsBenchmark : IDisposable
	{
		private RaxNewLog[][] _logs;

		private string[] _cats = new[] { "INFO", "DEBUG", "TRACE", "NEW_CAT", "ERROR", "WARNING", "CRITICAL" };

		public int N = 10;

		public int LOGS = 100_000;

		private RaxLogTable _table;

		public LogsBenchmark(bool manyMinutes, bool manyYears)
		{
			_logs = new RaxNewLog[N][];

			var rnd = new Random();

			var lastLogTime = DateTime.UtcNow;

			long totalLogIndex = 0;

			for (int i = 0; i < N; i++)
			{
				_logs[i] = new RaxNewLog[LOGS];
				for (int j = 0; j < LOGS; j++, ++totalLogIndex)
				{
					var local = new List<string>();

					var indx = rnd.Next(0, _cats.Length);
					if (rnd.NextDouble() > 0.5)
					{
						local.Add(_cats[indx]);
						var nindx = rnd.Next(0, _cats.Length);
						while (nindx == indx)
							nindx = rnd.Next(0, _cats.Length);
						indx = nindx;
					}
					local.Add(_cats[indx]);

					_logs[i][j] = new RaxNewLog
					{
						Categories = local.ToArray(),
						Message = $"LOG ENTRY {i}x{j} - {Guid.NewGuid():N}".ToCharArray(),
						Timestamp = lastLogTime.ToTimestamp()
					};

					if (manyMinutes && rnd.NextDouble() > 0.4)
						lastLogTime = lastLogTime.AddMinutes(rnd.Next(0, 60));
					else if (manyYears && totalLogIndex % 5000 == 0)
						lastLogTime = lastLogTime.AddMonths(3);
				}
			}

			string logsDir = $"_temp_bench_{N}_{LOGS}";
			if (!Directory.Exists(logsDir))
				Directory.CreateDirectory(logsDir);
			Directory.GetFiles(logsDir).ToList().ForEach(x => File.Delete(x));
			_table = RaxManager.OpenTable(logsDir);
		}

		public void WriteLogsBenchamrk()
		{
			foreach (var item in _logs)
			{
				_table.AppendLogs(item);
			}
		}

		public void Dispose()
		{
			_table.Dispose();
		}

		public static void Main(string[] args)
		{
			var times = new List<double>();

			var manyMinutes = args.Contains("-m");
			var manyYears = args.Contains("-y");

			void OneMinuteTest()
			{
				using var logs = new LogsBenchmark(manyMinutes, manyYears) { N = 10, LOGS = 100_000 };
				var sw = Stopwatch.StartNew();
				logs.WriteLogsBenchamrk();
				sw.Stop();
				times.Add(sw.Elapsed.TotalSeconds);
			}

			for (int i = 0; i < 5; i++)
			{
				OneMinuteTest();
			}

			var first = times.First();

			Console.WriteLine($"FIRST TIME = {first:00.000}s");
			Console.WriteLine($"AVG   TIME = {times.Skip(1).Average():00.000}s");
			Console.WriteLine($"MIN   TIME = {times.Skip(1).Min():00.000}s");
			Console.WriteLine($"MAX   TIME = {times.Skip(1).Max():00.000}s");
		}
	}
}
