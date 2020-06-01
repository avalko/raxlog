// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using BenchmarkDotNet.Running;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RaxLog.Core;
using RaxLog.Core.Helpers;
using RaxLog.Core.Structures;
using RaxLog.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace RaxLog.Tests
{
	[TestClass]
	public class LogsTests
	{
		const int ITERS = 10;
		const int LOGS_PER_ITER = 100_000;

		private RaxNewLog[][] _logsToAdd;
		private Random _rnd;
		private string[] _cats = new[] { "INFO", "DEBUG", "TRACE", "NEW_CAT", "ERROR", "WARNING", "CRITICAL" };
		private DateTime _beginTime, _endTime;

		public LogsTests()
		{
			_rnd = new Random(1);

			_beginTime = DateTime.UtcNow;
			var sw = Stopwatch.StartNew();
			_logsToAdd = new RaxNewLog[ITERS][];

			var lastLogTime = _beginTime;

			var totalLogIndex = 0;

			for (int i = 0; i < ITERS; i++)
			{
				_logsToAdd[i] = new RaxNewLog[LOGS_PER_ITER];
				for (int j = 0; j < LOGS_PER_ITER; j++, ++totalLogIndex)
				{
					var local = new List<string>();

					var indx = _rnd.Next(0, _cats.Length);
					if (_rnd.NextDouble() > 0.5)
					{
						local.Add(_cats[indx]);
						var nindx = _rnd.Next(0, _cats.Length);
						while (nindx == indx)
							nindx = _rnd.Next(0, _cats.Length);
						indx = nindx;
					}
					local.Add(_cats[indx]);

					_logsToAdd[i][j] = new RaxNewLog
					{
						Categories = local.ToArray(),
						Message = $"LOG ENTRY {i}x{j} - {Guid.NewGuid():N}".ToCharArray(),
						Timestamp = lastLogTime.ToTimestamp()
					};

					_endTime = lastLogTime;

					if (_rnd.NextDouble() > 0.4)
						lastLogTime = lastLogTime.AddMinutes(_rnd.Next(0, 60));
					else if (totalLogIndex % 2000 == 0)
						lastLogTime = lastLogTime.AddMonths(3);
				}
			}
			sw.Stop();

			Console.WriteLine($"MIN DATE = {_beginTime}");
			Console.WriteLine($"MAX DATE = {_endTime}");

			Console.WriteLine($"CREATE {ITERS * LOGS_PER_ITER}  LOGS = {sw.Elapsed}");
		}

		[TestMethod]
		public void FullTest()
		{
			Console.WriteLine("-- BEGIN FULL TEST -- ");

			const string dir = "full-test";

			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			Directory.GetFiles(dir).ToList().ForEach(x => File.Delete(x));

			using var table = RaxManager.OpenTable(dir);

			var sw = Stopwatch.StartNew();
			foreach (var item in _logsToAdd)
			{
				table.AppendLogs(item);
			}
			sw.Stop();
			Console.WriteLine($"WRITE ({ITERS * LOGS_PER_ITER}) LOGS = {sw.Elapsed}");

			sw = Stopwatch.StartNew();
			var allLogs = GetLogs(0, long.MaxValue);
			sw.Stop();
			Console.WriteLine($"READ  ({allLogs.Length}) LOGS = {sw.Elapsed}");

			var testIndex = LOGS_PER_ITER / 3;

			var rangedLogs = GetLogs(testIndex, 101);

			if (rangedLogs.Length != 101)
				throw new Exception("Выборка логов вернула больше или меньше, чем запрашивалось.");

			if (rangedLogs[0].Text != allLogs[testIndex].Text)
				throw new Exception("Выборка логов ошиблась в смещение (начало смещения).");
			if (rangedLogs[100].Text != allLogs[testIndex + 100].Text)
				throw new Exception("Выборка логов ошиблась в смещение (конец смещения).");

			if (allLogs.Length != ITERS * LOGS_PER_ITER + 1)
				throw new Exception("Количество прочитанных логов не соответсвует количеству добавленных.");
			if (allLogs[1].Date.ToTimestamp() != _beginTime.ToTimestamp())
				throw new Exception("Время первого лога не совпадает с действительным.");
			if (allLogs.Last().Date.ToTimestamp() != _endTime.ToTimestamp())
				throw new Exception("Время последнего лога не совпадает с действительным.");


			var dict = new Dictionary<string, int>();
			foreach (var log in allLogs)
			{
				foreach (var cat in log.Categories)
				{
					dict.TryGetValue(cat, out var prev);
					dict[cat] = prev + 1;
				}
			}
			var tableCategories = table.Categories;
			foreach (var cat in tableCategories)
			{
				if (!dict.TryGetValue(cat.Name, out var logsCount))
				{
					throw new Exception("В полученных логах нет одной из существующих категорий.");
				}
				if (logsCount != cat.LogsCount)
					throw new Exception("Количество полученных логов в одной из категорий не соответствует данным в таблице.");
			}
			foreach (var catname in dict.Keys)
			{
				if (!tableCategories.Any(x => x.Name == catname))
					throw new Exception("В таблице нет информации о какой-то категории.");
			}


			foreach (var log in allLogs)
			{
				if (log.Categories == null || log.Categories.Length == 0)
					throw new Exception("У какой-то записи лога нет категорий.");
				if (log.Date < _beginTime)
					throw new Exception("Время одного из полученных логов меньше времени первого лога.");
				if (log.Date > _endTime)
					throw new Exception("Время одного из полученных логов превышает время последнего лога.");
			}


			RaxLogEntry[] GetLogs(long skip, long take) => table.GetLogs(table.MinTimestamp, table.MaxTimestamp, skip, take);

			var metrics = RaxManager.GetMetricsSource(dir);

			foreach (var metric in metrics.GetAllMetrics())
			{
				Console.WriteLine($" -- [{metric.Key,-10}] = {metric.Value}");
			}

			var compressedPercents = 
				100.0 * metrics.GetMetricValue<long>(MetricsConstants.TOTAL_COMPRESSED_BYTES_COUNT_I64)
				/
				metrics.GetMetricValue<long>(MetricsConstants.TOTAL_PROCESSED_BYTES_COUNT_I64);

			Console.WriteLine($" -- [{'%',-10}] = {compressedPercents:00.###}%");

			Console.WriteLine("-- END FULL TEST -- ");
		}
	}
}
