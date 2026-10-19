namespace FalloutScriptStudio.ParserHost
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;

	/// <summary>
	/// Hosts the 32-bit native parser (<c>parser.dll</c>) in its own process so that a crash of the library only loses
	/// one request instead of the whole editor. See <see cref="HostProtocol"/> for the protocol: JSON lines on the standard input
	/// and output. Usage: <c>FalloutScriptStudio.ParserHost.exe [--parent &lt;process id&gt;]</c>.
	/// </summary>
	internal static class Program
	{
		private const String ParentOption = "--parent";

		private static async Task<Int32> Main(String[] args)
		{
			// A crash of the native library must end the process at once and silently (see CrashSilencer).
			CrashSilencer.Install();

			// The standard output belongs to the protocol. Managed code must not write to it, and whatever the native
			// library prints is skipped by the reader of the application (see HostProtocol.ResponsePrefix).
			Stream protocolOutput = Console.OpenStandardOutput();
			Console.SetOut(Console.Error);

			StartParentWatchdog(args);

			using var input = new StreamReader(Console.OpenStandardInput(), HostProtocol.Encoding, false, 1 << 16);
			using var output = new StreamWriter(protocolOutput, HostProtocol.Encoding, 1 << 16) { AutoFlush = false };
			using var engine = new NativeParser();

			await new NativeParserHost(new NativeCallGuard(engine)).RunAsync(input, output, CancellationToken.None).ConfigureAwait(false);
			return 0;
		}

		/// <summary>
		/// Ends the process when the application that started it disappears: a parse stuck inside the native library never
		/// notices the closed standard input.
		/// </summary>
		private static void StartParentWatchdog(String[] args)
		{
			Int32 index = Array.IndexOf(args, ParentOption);
			if (index < 0 || index + 1 >= args.Length || !Int32.TryParse(args[index + 1], out Int32 parentId))
			{
				return;
			}

			Process parent;
			try
			{
				parent = Process.GetProcessById(parentId);
			}
			catch (ArgumentException)
			{
				Environment.Exit(2);
				return;
			}

			var watchdog = new Thread(() =>
			{
				parent.WaitForExit();
				Environment.Exit(3);
			})
			{
				IsBackground = true,
				Name = "Parent watchdog",
			};
			watchdog.Start();
		}
	}
}