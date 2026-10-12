namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Threading.Tasks;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>
	/// Times an operation of a <c>Performance</c> test: runs it a few times, prints the minimum, median and maximum through the
	/// test output (<c>[perf] name: median ... ms</c>) and checks the median against a generous budget. This file is linked into both test projects.
	/// </summary>
	internal sealed class PerfMeter
	{
#if DEBUG
		/// <summary>A Debug build is several times slower than Release (and the budgets are set from Release timings).</summary>
		private const Double BudgetScale = 3.0;
#else
		private const Double BudgetScale = 1.0;
#endif

		private readonly ITestOutputHelper _output;

		/// <summary>Initializes a new instance of the <see cref="PerfMeter"/> class.</summary>
		/// <param name="output">Receives the lines of the timings.</param>
		public PerfMeter(ITestOutputHelper output)
		{
			this._output = output;
		}

		/// <summary>Gets the peak working set of the process in megabytes.</summary>
		public static Double PeakWorkingSetMegabytes
		{
			get
			{
				using Process process = Process.GetCurrentProcess();
				process.Refresh();
				return process.PeakWorkingSet64 / (1024.0 * 1024.0);
			}
		}

		/// <summary>Gets the current managed heap size in megabytes (after nothing is forced to collect).</summary>
		public static Double ManagedMegabytes => GC.GetTotalMemory(false) / (1024.0 * 1024.0);

		/// <summary>Runs an operation several times, prints the timings and checks the median against a budget.</summary>
		/// <param name="name">What is measured.</param>
		/// <param name="budgetMilliseconds">The median must stay below this (several times the measured time).</param>
		/// <param name="action">The operation.</param>
		/// <param name="iterations">The number of measured runs.</param>
		/// <returns>The median in milliseconds.</returns>
		public Double Measure(String name, Double budgetMilliseconds, Action action, Int32 iterations = 5)
		{
			action();
			List<Double> times = new List<Double>(iterations);
			for (Int32 i = 0; i < iterations; i++)
			{
				Stopwatch watch = Stopwatch.StartNew();
				action();
				watch.Stop();
				times.Add(watch.Elapsed.TotalMilliseconds);
			}

			return this.Report(name, budgetMilliseconds, times);
		}

		/// <summary>Runs an asynchronous operation several times, prints the timings and checks the median against a budget.</summary>
		/// <param name="name">What is measured.</param>
		/// <param name="budgetMilliseconds">The median must stay below this.</param>
		/// <param name="action">The operation.</param>
		/// <param name="iterations">The number of measured runs.</param>
		/// <returns>The median in milliseconds.</returns>
		public async Task<Double> MeasureAsync(String name, Double budgetMilliseconds, Func<Task> action, Int32 iterations = 5)
		{
			await action();
			List<Double> times = new List<Double>(iterations);
			for (Int32 i = 0; i < iterations; i++)
			{
				Stopwatch watch = Stopwatch.StartNew();
				await action();
				watch.Stop();
				times.Add(watch.Elapsed.TotalMilliseconds);
			}

			return this.Report(name, budgetMilliseconds, times);
		}

		/// <summary>Times a single run (no warm-up): for operations that cannot be repeated.</summary>
		/// <param name="name">What is measured.</param>
		/// <param name="budgetMilliseconds">The time must stay below this.</param>
		/// <param name="action">The operation.</param>
		/// <returns>The time in milliseconds.</returns>
		public Double MeasureOnce(String name, Double budgetMilliseconds, Action action)
		{
			Stopwatch watch = Stopwatch.StartNew();
			action();
			watch.Stop();
			return this.Report(name, budgetMilliseconds, new List<Double> { watch.Elapsed.TotalMilliseconds });
		}

		/// <summary>Prints a free line of the report.</summary>
		/// <param name="text">The text.</param>
		public void Note(String text)
		{
			this._output.WriteLine("[perf] " + text);
		}

		private Double Report(String name, Double budgetMilliseconds, List<Double> times)
		{
			budgetMilliseconds *= BudgetScale;
			times.Sort();
			Double median = times[times.Count / 2];
			this._output.WriteLine(String.Format(
				CultureInfo.InvariantCulture,
				"[perf] {0}: median {1:F2} ms (min {2:F2}, max {3:F2}, n={4}), budget {5:F0} ms",
				name,
				median,
				times[0],
				times[times.Count - 1],
				times.Count,
				budgetMilliseconds));
			Assert.True(median < budgetMilliseconds, String.Format(CultureInfo.InvariantCulture, "{0} took {1:F1} ms (budget {2:F0} ms)", name, median, budgetMilliseconds));
			return median;
		}
	}
}
