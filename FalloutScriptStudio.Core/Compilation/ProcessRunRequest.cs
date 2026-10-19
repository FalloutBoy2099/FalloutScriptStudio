namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// Describes an external process to run.
	/// </summary>
	public sealed record ProcessRunRequest
	{
		/// <summary>Gets the executable or batch file to start.</summary>
		public required String FileName { get; init; }

		/// <summary>Gets the argument string.</summary>
		public String Arguments { get; init; } = String.Empty;

		/// <summary>Gets the working directory, or <see langword="null"/> to inherit the current one.</summary>
		public String? WorkingDirectory { get; init; }

		/// <summary>Gets a value indicating whether the standard output is captured.</summary>
		public Boolean CaptureOutput { get; init; } = true;

		/// <summary>Gets the encoding of the standard output, or <see langword="null"/> for the Windows ANSI code page (what the legacy editor used).</summary>
		public Encoding? OutputEncoding { get; init; }

		/// <summary>Gets environment variables that are set for the process in addition to the inherited ones, or <see langword="null"/> for none.</summary>
		public IReadOnlyDictionary<String, String>? Environment { get; init; }

		/// <summary>Gets an optional receiver of the output lines while the process is running.</summary>
		public IProgress<String>? OutputLineProgress { get; init; }
	}
}