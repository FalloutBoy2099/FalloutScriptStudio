namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.IO;

	/// <summary>
	/// Configuration of <see cref="OutOfProcessNativeParser"/>.
	/// </summary>
	public sealed record NativeParserHostOptions
	{
		/// <summary>The file name of the parser host executable.</summary>
		public const String HostFileName = "FalloutScriptStudio.ParserHost.exe";

		/// <summary>Gets the full path of the host executable; <c>null</c> or a missing file makes the native parser unavailable.</summary>
		public String? HostPath { get; init; }

		/// <summary>Gets how long a single request (including the start of a new host) may take before the host is killed.</summary>
		public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

		/// <summary>
		/// Gets the number of requests after which the host is replaced by a fresh process, to bound whatever the native library
		/// leaks; 0 never recycles the host.
		/// </summary>
		public Int32 RecycleAfterRequests { get; init; } = 500;

		/// <summary>
		/// Gets how long the parser reports itself as unavailable after hosts keep dying before answering anything (for example when
		/// the 32-bit .NET runtime is missing), instead of starting a doomed process for every keystroke.
		/// </summary>
		public TimeSpan StartFailureBackoff { get; init; } = TimeSpan.FromSeconds(15);

		/// <summary>
		/// Gets how many parses in a row may end in a crash or hang of <c>parser.dll</c> (each with a different text) before the native
		/// parser is turned off for the rest of the session and the built-in parser takes over; 0 never turns it off. A parse that works
		/// resets the count.
		/// </summary>
		public Int32 MaxConsecutiveCrashes { get; init; } = 3;

		/// <summary>Gets the folder in which the private working folders of the hosts are created; the system temp folder when <c>null</c>.</summary>
		public String? WorkingDirectoryRoot { get; init; }

		/// <summary>Creates the options for a host that sits in a folder (normally the folder of the application).</summary>
		/// <param name="directory">The folder that contains <see cref="HostFileName"/>.</param>
		/// <returns>The options.</returns>
		public static NativeParserHostOptions ForDirectory(String directory)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(directory);
			return new NativeParserHostOptions { HostPath = Path.Combine(directory, HostFileName) };
		}
	}
}