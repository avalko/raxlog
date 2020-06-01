// Copyright 2020 Artem Valko.
// Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for license information.
// See NOTICE  in the project root for for additional information regarding copyright ownership.

using BenchmarkDotNet.Running;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace RaxLog.Tests
{
	[TestClass]
	public class BenchmarkTests
	{
		class SBWriter : TextWriter
		{
			private StringBuilder _sb = new StringBuilder();

			public override Encoding Encoding { get; } = Encoding.UTF8;

			public override void WriteLine(string value)
			{
				Write(value);
				WriteLine();
			}

			public override void WriteLine(char value)
			{
				Write(value);
				WriteLine();
			}

			public override void WriteLine()
			{
				_sb.AppendLine();
			}

			public override void Write(char value)
			{
				_sb.Append(value);
			}

			public override void Write(string value)
			{
				_sb.Append(value);
			}

			public override string ToString()
			{
				return _sb.ToString();
			}
		}

		private string RunDotnet(string args)
		{
			var proc = new Process();
			proc.StartInfo = new ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = args,
				RedirectStandardOutput = true,
			};
			proc.Start();
			proc.WaitForExit();

			return proc.StandardOutput.ReadToEnd();
		}

		[TestMethod]
		public void Benchmark()
		{
			var asm = Assembly.GetExecutingAssembly().Location;

			Console.WriteLine("ONE MINUTE");
			Console.WriteLine(RunDotnet($"{asm}"));

			Console.WriteLine("MANY MINUTES");
			Console.WriteLine(RunDotnet($"{asm} -m"));

			Console.WriteLine("MANY YEARS");
			Console.WriteLine(RunDotnet($"{asm} -m -y"));
		}
	}
}
