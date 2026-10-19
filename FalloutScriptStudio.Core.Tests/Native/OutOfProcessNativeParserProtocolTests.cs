namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using Xunit;

	/// <summary>Request and answer protocol of <see cref="OutOfProcessNativeParser"/> with simulated hosts.</summary>
	[Collection("Test collection for FalloutScriptStudio.Core.Tests.Native.OutOfProcessNativeParserTests")]
	public sealed class OutOfProcessNativeParserProtocolTests : OutOfProcessNativeParserTestBase
	{
		[Fact]
		public async Task ParseAsync_ParserLibraryNotConfigured_IsUnavailableWithoutStartingAHost()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);

			NativeParseResult result = await this._parser!.ParseAsync("x", @"C:\s\a.ssl", new ParserOptions(), CancellationToken.None);

			Assert.Equal(NativeParseStatus.Unavailable, result.Status);
			Assert.Contains("not configured", result.Message, StringComparison.Ordinal);
			Assert.Empty(launcher.Started);
		}

		[Fact]
		public async Task ParseAsync_ParserLibraryMissing_IsUnavailable()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);

			NativeParseResult result = await this._parser!.ParseAsync("x", @"C:\s\a.ssl", new ParserOptions { NativeParserPath = Path.Combine(this._directory.Path, "nope.dll") }, CancellationToken.None);

			Assert.Equal(NativeParseStatus.Unavailable, result.Status);
			Assert.Contains("was not found", result.Message, StringComparison.Ordinal);
			Assert.Empty(launcher.Started);
		}

		[Fact]
		public async Task ParseAsync_HostExecutableNotConfiguredOrMissing_IsUnavailable()
		{
			String library = this._directory.Write("parser.dll", "x");
			var options = new ParserOptions { NativeParserPath = library };
			var launcher = new FakeParserHostLauncher(Normal);

			using var notConfigured = new OutOfProcessNativeParser(new NativeParserHostOptions(), null, launcher);
			using var missing = new OutOfProcessNativeParser(new NativeParserHostOptions { HostPath = Path.Combine(this._directory.Path, "nohost.exe") }, null, launcher);

			NativeParseResult first = await notConfigured.ParseAsync("x", @"C:\s\a.ssl", options, CancellationToken.None);
			NativeParseResult second = await missing.ParseAsync("x", @"C:\s\a.ssl", options, CancellationToken.None);

			Assert.Equal(NativeParseStatus.Unavailable, first.Status);
			Assert.Contains("host", first.Message, StringComparison.Ordinal);
			Assert.Equal(NativeParseStatus.Unavailable, second.Status);
			Assert.Contains("nohost.exe", second.Message, StringComparison.Ordinal);
			Assert.Empty(launcher.Started);
		}

		[Fact]
		public async Task ParseAsync_PreCancelledToken_ThrowsWithoutStartingAHost()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => this.ParseAsync(cts.Token));

			Assert.Empty(launcher.Started);
		}

		[Fact]
		public async Task ParseAsync_SendsTheScriptAndTheOptionsAndReturnsTheDecodedResult()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);

			NativeParseResult result = await this.ParseAsync(default, "procedure start begin\r\nend \u00e9\r\n");

			Assert.Equal(2, launcher.Started[0].Requests.Count);
			Assert.Equal(HostProtocol.PingCommand, HostProtocol.DeserializeRequest(launcher.Started[0].Requests[0]).Command);
			HostRequest request = HostProtocol.DeserializeRequest(launcher.Started[0].Requests[1]);
			Assert.Equal(HostProtocol.ParseCommand, request.Command);
			Assert.Equal(HostProtocol.Version, request.Version);
			Assert.Equal("procedure start begin\r\nend \u00e9\r\n", request.ScriptText);
			Assert.Equal(Path.Combine(this._directory.Path, "scripts", "a.ssl"), request.FilePath);
			Assert.Equal(Path.Combine(this._directory.Path, "resources", "parser.dll"), request.ParserLibraryPath);
			Assert.Equal("FOO=1", request.PreprocessorDefinitions);
			Assert.Equal(@"C:\headers", request.HeadersDirectory);
			Assert.Equal(1, request.BackwardCompatibilityMode);
			Assert.Equal(@"C:\scr", request.ScratchDirectory);
			Assert.Equal("p.ssl", request.ScratchFileName);
			Assert.DoesNotContain('\n', launcher.Started[0].Requests[1]);

			Assert.Equal(NativeParseStatus.Completed, result.Status);
			Assert.Equal(-1, result.ExitCode);
			Assert.True(result.HasData);
			Assert.False(result.Succeeded);
			Assert.Equal("[Error] <Semantic> <C:\\s\\a.ssl>:1:2: text \"quoted\" \u00e9\r\n", result.Log);

			ScriptProcedure procedure = Assert.Single(result.Procedures);
			Assert.Equal("start", procedure.Name);
			Assert.Equal(ProcedureFlags.Export | ProcedureFlags.Critical, procedure.Flags);
			Assert.Equal(@"C:\s\a.ssl", procedure.DeclaredFile);
			Assert.Equal(@"C:\s\b.h", procedure.StartFile);
			Assert.Equal("a.ssl", procedure.FileName);
			Assert.Equal((3, 9, 9, 20), (procedure.DeclaredLine, procedure.DefinedLine, procedure.StartLine, procedure.EndLine));
			Assert.Equal((1, 4), (procedure.ArgumentCount, procedure.TimeValue));
			Assert.Equal(new[] { new SourceReference(@"C:\s\a.ssl", 30), new SourceReference(@"C:\s\b.h", 2) }, procedure.References);
			ScriptVariable argument = Assert.Single(procedure.Variables);
			Assert.Equal(("who", VariableScope.Local, 5, 9, "\"x\"", ValueKind.String), (argument.Name, argument.Scope, argument.DeclaredLine, argument.ArgumentDeclaredLine, argument.InitialValue, argument.InitialValueKind));
			Assert.Equal(new SourceReference(@"C:\s\a.ssl", 12), Assert.Single(argument.References));

			ScriptVariable global = Assert.Single(result.Variables);
			Assert.Equal(("g_count", VariableScope.Global, "1.5", ValueKind.Float, "a.ssl"), (global.Name, global.Scope, global.InitialValue, global.InitialValueKind, global.FileName));
		}

		[Fact]
		public async Task ParseAsync_SeveralRequests_ReuseOneHostAndPingItOnce()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);

			await this.ParseAsync();
			await this.ParseAsync();
			NativeParseResult third = await this.ParseAsync();

			Assert.True(third.HasData);
			FakeParserHost host = Assert.Single(launcher.Started);
			Assert.Equal(4, host.Requests.Count);
			Assert.Equal(new[] { HostProtocol.PingCommand, HostProtocol.ParseCommand, HostProtocol.ParseCommand, HostProtocol.ParseCommand }, host.Requests.Select(r => HostProtocol.DeserializeRequest(r).Command).ToArray());
			Assert.Equal(new Int64[] { 1, 2, 3, 4 }, host.Requests.Select(r => HostProtocol.DeserializeRequest(r).Id).ToArray());
			Assert.False(host.Killed);
		}

		[Fact]
		public async Task ParseAsync_ConcurrentRequests_AreServedOneAtATime()
		{
			Int32 active = 0;
			Int32 maximum = 0;
			FakeParserHostLauncher launcher = this.Create(index => Scripted((host, request) =>
			{
				Int32 now = Interlocked.Increment(ref active);
				maximum = Math.Max(maximum, now);
				Task.Delay(40).ContinueWith(
					done =>
					{
						Interlocked.Decrement(ref active);
						host.Respond(request, SampleResult());
					},
					TaskScheduler.Default);
			}));

			NativeParseResult[] results = await Task.WhenAll(this.ParseAsync(), this.ParseAsync(), this.ParseAsync(), this.ParseAsync());

			Assert.All(results, r => Assert.True(r.HasData));
			Assert.Equal(1, maximum);
			Assert.Single(launcher.Started);
		}

		[Fact]
		public async Task ParseAsync_HostGetsAPrivateWorkingFolderThatIsRemovedWhenTheHostIsDropped()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);

			await this.ParseAsync();
			String directory = Assert.Single(launcher.WorkingDirectories);
			Boolean existedWhileRunning = Directory.Exists(directory);
			this._parser!.Dispose();

			Assert.True(existedWhileRunning);
			Assert.StartsWith(this._directory.Path, directory, StringComparison.OrdinalIgnoreCase);
			Assert.False(Directory.Exists(directory));
		}

		[Fact]
		public async Task ParseAsync_InvalidScriptPath_FailsWithoutThrowing()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);

			NativeParseResult result = await this._parser!.ParseAsync("x", "bad\0path.ssl", this.Options(), CancellationToken.None);

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("script path is invalid", result.Message, StringComparison.Ordinal);
			Assert.Empty(launcher.Started);
		}
	}
}