namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>
	/// The state shared by the smoke tests of one run: the output folder (pictures and report), and the private copies of the built program
	/// (so that the real build output is never locked or altered). Nothing is created until a test needs it, so that a run in which every
	/// test is skipped costs nothing.
	/// </summary>
	public sealed class UiSmokeSuite : IDisposable
	{
		private readonly Object _gate = new();
		private readonly List<String> _copies = [];
		private String? _normalCopy;
		private String? _withoutParserCopy;
		private String? _publishCopy;
		private StreamWriter? _report;

		/// <summary>Gets the folder of the pictures and the report.</summary>
		internal String OutputDirectory { get; } = UiSmokeEnvironment.OutputDirectory;

		/// <summary>Gets the folder of the private copy of the program (created on first use).</summary>
		internal String ProgramCopy
		{
			get
			{
				lock (this._gate)
				{
					this._normalCopy ??= this.CreateCopy(removeParser: false);
					return this._normalCopy;
				}
			}
		}

		/// <summary>Gets the folder of a private copy of the program whose <c>parser.dll</c> is missing (created on first use).</summary>
		internal String ProgramCopyWithoutParser
		{
			get
			{
				lock (this._gate)
				{
					this._withoutParserCopy ??= this.CreateCopy(removeParser: true);
					return this._withoutParserCopy;
				}
			}
		}

		/// <summary>Gets the folder of a private copy of the published executables (created on first use).</summary>
		internal String PublishCopy
		{
			get
			{
				lock (this._gate)
				{
					if (this._publishCopy is null)
					{
						this._publishCopy = TestFiles.CreateWorkFolder("ui-smoke-app-publish");
						this._copies.Add(this._publishCopy);
						CopyFolder(UiSmokeEnvironment.PublishDirectory, this._publishCopy);
					}

					return this._publishCopy;
				}
			}
		}

		/// <summary>Writes a line to the report.</summary>
		/// <param name="test">The test.</param>
		/// <param name="text">The line.</param>
		internal void Report(String test, String text)
		{
			lock (this._gate)
			{
				if (this._report is null)
				{
					Directory.CreateDirectory(this.OutputDirectory);
					this._report = new StreamWriter(Path.Combine(this.OutputDirectory, "report.txt"), append: false) { AutoFlush = true };
					this._report.WriteLine("Fallout Script Studio - UI smoke report, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
					this._report.WriteLine("Program: " + UiSmokeEnvironment.AppDirectory);
				}

				this._report.WriteLine(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " [" + test + "] " + text);
			}
		}

		/// <summary>Removes the copies of the program and closes the report.</summary>
		public void Dispose()
		{
			lock (this._gate)
			{
				this._report?.Dispose();
				this._report = null;
				foreach (String copy in this._copies)
				{
					TestFiles.DeleteWorkFolder(copy);
				}

				this._copies.Clear();
			}
		}

		private String CreateCopy(Boolean removeParser)
		{
			String target = TestFiles.CreateWorkFolder(removeParser ? "ui-smoke-app-noparser" : "ui-smoke-app");
			this._copies.Add(target);
			CopyFolder(UiSmokeEnvironment.AppDirectory, target);

			// The real compiler of the sample tools is made available to the copy (the program looks for it in resources).
			String compiler = Path.Combine(TestFiles.Tools, "compile.exe");
			String resources = Path.Combine(target, "resources");
			if (File.Exists(compiler) && Directory.Exists(resources))
			{
				File.Copy(compiler, Path.Combine(resources, "compile.exe"), true);
			}

			if (removeParser)
			{
				File.Delete(Path.Combine(resources, "parser.dll"));
			}

			return target;
		}

		private static void CopyFolder(String source, String target)
		{
			foreach (String directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
			}

			foreach (String file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
			{
				String relative = Path.GetRelativePath(source, file);
				if (IsRuntimeState(relative))
				{
					continue;
				}

				File.Copy(file, Path.Combine(target, relative), true);
			}
		}

		private static Boolean IsRuntimeState(String relative)
		{
			String first = relative.Split(Path.DirectorySeparatorChar)[0];
			return new[] { "settings", "scrTemp" }.Contains(first, StringComparer.OrdinalIgnoreCase)
				|| relative.Equals("sse.log", StringComparison.OrdinalIgnoreCase)
				|| relative.Equals("sse.log.1", StringComparison.OrdinalIgnoreCase);
		}
	}
}