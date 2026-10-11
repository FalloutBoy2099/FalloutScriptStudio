namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using FalloutScriptStudio.Core.Tests.Parsing;
	using Xunit;

	/// <summary>
	/// Tests with the real <c>FalloutScriptStudio.ParserHost.exe</c> process: start-up, shutdown, watchdog, kill on timeout and on
	/// cancellation. They need the host built next to the tests and <c>parser.dll</c> for the timeout and cancellation cases.
	/// </summary>
	public sealed class ParserHostProcessTests : IDisposable
	{
		private readonly TempDirectory _directory = new TempDirectory();

		public void Dispose()
		{
			this._directory.Dispose();
		}

		private sealed class RecordingLauncher : IParserHostLauncher
		{
			private readonly ProcessParserHostLauncher _inner = new ProcessParserHostLauncher();

			public List<IParserHostProcess> Started { get; } = new List<IParserHostProcess>();

			public IParserHostProcess Start(String hostPath, String workingDirectory)
			{
				IParserHostProcess host = this._inner.Start(hostPath, workingDirectory);
				this.Started.Add(host);
				return host;
			}
		}

		/// <summary>Asks the operating system (not the wrapper) whether the host process is still running.</summary>
		private static Boolean IsAlive(IParserHostProcess host)
		{
			try
			{
				using Process process = Process.GetProcessById(((ProcessParserHost)host).ProcessId);
				return !process.HasExited;
			}
			catch (ArgumentException)
			{
				return false;
			}
		}

		private static async Task<Int32> WaitForExitCodeAsync(Process process)
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
			await process.WaitForExitAsync(cts.Token);
			return process.ExitCode;
		}

		private static Process StartHost(params String[] arguments)
		{
			var startInfo = new ProcessStartInfo(SslcTools.HostExe)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
			};
			startInfo.Environment.Remove("DOTNET_ROOT");
			foreach (String argument in arguments)
			{
				startInfo.ArgumentList.Add(argument);
			}

			return Process.Start(startInfo) ?? throw new InvalidOperationException("The host did not start.");
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Host_AnswersAPingAndEndsWhenItsInputIsClosed()
		{
			var launcher = new ProcessParserHostLauncher();
			using IParserHostProcess host = launcher.Start(SslcTools.HostExe, this._directory.Path);

			await host.Input.WriteLineAsync(HostProtocol.SerializeRequest(new HostRequest { Id = 21, Command = HostProtocol.PingCommand }));
			await host.Input.FlushAsync();
			String? line = await host.Output.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
			host.Input.Close();
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
			await host.WaitForExitAsync(cts.Token);

			Assert.NotNull(line);
			Assert.True(HostProtocol.TryReadResponse(line!, out HostResponse? response));
			Assert.Equal((21L, "Completed"), (response!.Id, response.Status));
			Assert.Equal(0, host.ExitCode);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Host_ExitCommand_EndsTheProcess()
		{
			using Process process = StartHost();

			await process.StandardInput.WriteLineAsync(HostProtocol.SerializeRequest(new HostRequest { Id = 1, Command = HostProtocol.ExitCommand }));
			await process.StandardInput.FlushAsync();

			Assert.Equal(0, await WaitForExitCodeAsync(process));
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Host_ParentAlreadyGone_ExitsAtOnce()
		{
			using Process parent = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { CreateNoWindow = true, UseShellExecute = false })!;
			Int32 parentId = parent.Id;
			await parent.WaitForExitAsync();
			using Process process = StartHost("--parent", parentId.ToString(System.Globalization.CultureInfo.InvariantCulture));

			Assert.Equal(2, await WaitForExitCodeAsync(process));
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Host_ParentDisappears_EndsEvenWhileItsInputIsStillOpen()
		{
			using Process parent = Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!;
			using Process process = StartHost("--parent", parent.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
			await Task.Delay(500);
			Assert.False(process.HasExited);

			parent.Kill();

			Assert.Equal(3, await WaitForExitCodeAsync(process));
		}

		[RealToolFact(RealTools.Parser)]
		public async Task ParseAsync_RealHostTooSlow_IsKilledOnTimeout()
		{
			var launcher = new RecordingLauncher();
			using var fixture = new NativeParserFixture();
			using var parser = new OutOfProcessNativeParser(new NativeParserHostOptions { HostPath = SslcTools.HostExe, WorkingDirectoryRoot = this._directory.Combine("hostroot"), RequestTimeout = TimeSpan.FromMilliseconds(1) }, null, launcher);

			NativeParseResult result = await parser.ParseAsync("procedure start begin\r\nend\r\n", fixture.Write("scripts\\t.ssl", "x"), fixture.CreateOptions(), CancellationToken.None);

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("did not answer", result.Message, StringComparison.Ordinal);
			IParserHostProcess host = Assert.Single(launcher.Started);
			Assert.False(IsAlive(host));
		}

		[RealToolFact(RealTools.Parser)]
		public async Task ParseAsync_RealHostCancelled_IsKilledAndAFreshHostServesTheNextRequest()
		{
			var launcher = new RecordingLauncher();
			using var fixture = new NativeParserFixture();
			using var parser = new OutOfProcessNativeParser(new NativeParserHostOptions { HostPath = SslcTools.HostExe, WorkingDirectoryRoot = this._directory.Combine("hostroot") }, null, launcher);
			String script = fixture.Write("scripts\\c.ssl", "x");
			using var cts = new CancellationTokenSource();

			Task<NativeParseResult> cancelled = parser.ParseAsync("procedure start begin\r\nend\r\n", script, fixture.CreateOptions(), cts.Token);
			for (Int32 wait = 0; wait < 500 && launcher.Started.Count == 0; wait++)
			{
				await Task.Delay(10);
			}

			cts.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
			NativeParseResult next = await parser.ParseAsync("procedure start begin\r\nend\r\n", script, fixture.CreateOptions(), CancellationToken.None);

			Assert.Equal(2, launcher.Started.Count);
			Assert.False(IsAlive(launcher.Started[0]));
			Assert.True(next.Succeeded, next.Message + next.Log);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task ParseAsync_RealHostIsStoppedWhenTheParserIsDisposed()
		{
			var launcher = new RecordingLauncher();
			using var fixture = new NativeParserFixture();
			var parser = new OutOfProcessNativeParser(new NativeParserHostOptions { HostPath = SslcTools.HostExe, WorkingDirectoryRoot = this._directory.Combine("hostroot") }, null, launcher);
			String script = fixture.Write("scripts\\d.ssl", "x");
			await parser.ParseAsync("procedure start begin\r\nend\r\n", script, fixture.CreateOptions(), CancellationToken.None);
			IParserHostProcess host = Assert.Single(launcher.Started);
			Assert.True(IsAlive(host));

			parser.Dispose();

			Assert.False(IsAlive(host));
		}

		[RealToolFact(RealTools.Parser)]
		public async Task ParseAsync_HostWorkingFolder_ReceivesTheNativeLogAndIsRemoved()
		{
			var launcher = new RecordingLauncher();
			using var fixture = new NativeParserFixture();
			String root = Path.Combine(this._directory.Path, "hostroot");
			using var parser = new OutOfProcessNativeParser(new NativeParserHostOptions { HostPath = SslcTools.HostExe, WorkingDirectoryRoot = root }, null, launcher);
			String script = fixture.Write("scripts\\e.ssl", "x");

			NativeParseResult result = await parser.ParseAsync(SslSamples.SyntaxError, script, fixture.CreateOptions(), CancellationToken.None);

			Assert.Equal(1, result.ExitCode);
			Assert.Contains("[Error]", result.Log, StringComparison.Ordinal);
			parser.Dispose();
			Assert.Empty(Directory.GetDirectories(Path.Combine(root, "FalloutScriptStudio")));
		}
	}
}
