namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using FalloutScriptStudio.Core.Tests.Parsing;
	using Xunit;

	/// <summary>Protocol, crash handling, timeouts and cancellation of <see cref="OutOfProcessNativeParser"/> with simulated hosts.</summary>
	public sealed class OutOfProcessNativeParserTests : IDisposable
	{
		private const Int32 AccessViolation = unchecked((Int32)0xC0000005);

		private readonly TempDirectory _directory = new TempDirectory();
		private OutOfProcessNativeParser? _parser;
		private Int32 _textCounter;

		public void Dispose()
		{
			this._parser?.Dispose();
			this._directory.Dispose();
		}

		private static NativeParseResult SampleResult()
		{
			var argument = new ScriptVariable("who", VariableScope.Local)
			{
				DeclaredFile = @"C:\s\a.ssl",
				DeclaredLine = 5,
				ArgumentDeclaredLine = 9,
				InitialValue = "\"x\"",
				InitialValueKind = ValueKind.String,
				References = new[] { new SourceReference(@"C:\s\a.ssl", 12) },
			};
			var procedure = new ScriptProcedure("start")
			{
				Flags = ProcedureFlags.Export | ProcedureFlags.Critical,
				DeclaredFile = @"C:\s\a.ssl",
				StartFile = @"C:\s\b.h",
				FileName = "a.ssl",
				DeclaredLine = 3,
				DefinedLine = 9,
				StartLine = 9,
				EndLine = 20,
				ArgumentCount = 1,
				TimeValue = 4,
				Variables = new[] { argument },
				References = new[] { new SourceReference(@"C:\s\a.ssl", 30), new SourceReference(@"C:\s\b.h", 2) },
			};
			var global = new ScriptVariable("g_count", VariableScope.Global)
			{
				DeclaredFile = @"C:\s\a.ssl",
				FileName = "a.ssl",
				DeclaredLine = 2,
				InitialValue = "1.5",
				InitialValueKind = ValueKind.Float,
				ArrayLength = 0,
			};
			return new NativeParseResult(NativeParseStatus.Completed)
			{
				ExitCode = -1,
				Procedures = new[] { procedure },
				Variables = new[] { global },
				Log = "[Error] <Semantic> <C:\\s\\a.ssl>:1:2: text \"quoted\" \u00e9\r\n",
			};
		}

		private static FakeParserHost Normal(Int32 index)
		{
			return new FakeParserHost(AnswerNormally);
		}

		private static void AnswerNormally(FakeParserHost host, String line)
		{
			HostRequest request = HostProtocol.DeserializeRequest(line);
			if (request.Command == HostProtocol.ParseCommand)
			{
				host.Respond(request, SampleResult());
			}
			else
			{
				host.Respond(request, new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 0 });
			}
		}

		/// <summary>A host that answers pings and calls <paramref name="onParse"/> for parse requests.</summary>
		private static FakeParserHost Scripted(Action<FakeParserHost, HostRequest> onParse)
		{
			return new FakeParserHost((host, line) =>
			{
				HostRequest request = HostProtocol.DeserializeRequest(line);
				if (request.Command == HostProtocol.ParseCommand)
				{
					onParse(host, request);
				}
				else
				{
					host.Respond(request, new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 0 });
				}
			});
		}

		private FakeParserHostLauncher Create(Func<Int32, FakeParserHost> factory, NativeParserHostOptions? options = null, TimeProvider? time = null)
		{
			var launcher = new FakeParserHostLauncher(factory);
			String hostPath = this._directory.Write("host\\FalloutScriptStudio.ParserHost.exe", "x");
			this._parser = new OutOfProcessNativeParser(
				(options ?? new NativeParserHostOptions()) with { HostPath = hostPath, WorkingDirectoryRoot = this._directory.Path },
				null,
				launcher,
				time);
			return launcher;
		}

		private ParserOptions Options()
		{
			String library = Path.Combine(this._directory.Path, "resources", "parser.dll");
			if (!File.Exists(library))
			{
				this._directory.Write(Path.Combine("resources", "parser.dll"), "x");
			}

			return new ParserOptions
			{
				NativeParserPath = library,
				PreprocessorDefinitions = "FOO=1",
				HeadersDirectory = @"C:\headers",
				BackwardCompatibilityMode = 1,
				ScratchDirectory = @"C:\scr",
				ScratchFileName = "p.ssl",
			};
		}

		private Task<NativeParseResult> ParseAsync(CancellationToken cancellationToken = default, String? text = null)
		{
			// Every call parses another text unless the test says otherwise: a text that crashed the host is not sent again.
			text ??= "procedure start" + (++this._textCounter).ToString(System.Globalization.CultureInfo.InvariantCulture) + " begin\r\nend\r\n";
			return this._parser!.ParseAsync(text, Path.Combine(this._directory.Path, "scripts", "a.ssl"), this.Options(), cancellationToken);
		}

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
		public async Task ParseAsync_HostCrashesWhileParsing_ReportsTheExitCodeAndTheNextRequestUsesANewHost()
		{
			FakeParserHostLauncher launcher = this.Create(index => index == 0
				? Scripted((host, request) =>
				{
					host.StandardErrorTail = "Fatal error. 0xC0000005";
					host.Exit(AccessViolation);
				})
				: Normal(index));

			NativeParseResult crashed = await this.ParseAsync();
			NativeParseResult recovered = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, crashed.Status);
			Assert.False(crashed.HasData);
			Assert.Contains("stopped unexpectedly", crashed.Message, StringComparison.Ordinal);
			Assert.Contains("0xC0000005", crashed.Message, StringComparison.Ordinal);
			Assert.Contains("access violation", crashed.Message, StringComparison.Ordinal);
			Assert.Contains("Fatal error", crashed.Message, StringComparison.Ordinal);
			Assert.True(launcher.Started[0].Disposed);
			Assert.Equal(2, launcher.Started.Count);
			Assert.True(recovered.HasData);
		}

		[Fact]
		public async Task ParseAsync_HostReportsAFatalErrorButStaysAlive_IsKilledWithoutWaitingForTheTimeout()
		{
			// With Windows Error Reporting disabled a crashed process may hang for ever after the runtime printed its report.
			FakeParserHostLauncher launcher = this.Create(
				index => index == 0
					? Scripted((host, request) => host.ReportFatalError("Fatal error.\r\n0xC0000005\r\n   at Something.ParseMain()"))
					: Normal(index),
				new NativeParserHostOptions { RequestTimeout = TimeSpan.FromSeconds(60) });

			var watch = System.Diagnostics.Stopwatch.StartNew();
			NativeParseResult crashed = await this.ParseAsync();
			TimeSpan elapsed = watch.Elapsed;
			NativeParseResult recovered = await this.ParseAsync();

			Assert.True(elapsed < TimeSpan.FromSeconds(10), "Took " + elapsed);
			Assert.Equal(NativeParseStatus.Failed, crashed.Status);
			Assert.Contains("stopped unexpectedly", crashed.Message, StringComparison.Ordinal);
			Assert.Contains("0xC0000005, access violation", crashed.Message, StringComparison.Ordinal);
			Assert.Contains("Fatal error.", crashed.Message, StringComparison.Ordinal);
			Assert.True(launcher.Started[0].Killed);
			Assert.True(recovered.HasData);
		}

		[Fact]
		public async Task ParseAsync_HostCrashesWhileParsing_IsNotAStartFailureAndWithoutABreakerKeepsRestarting()
		{
			FakeParserHostLauncher launcher = this.Create(_ => Scripted((host, request) => host.Exit(AccessViolation)), new NativeParserHostOptions { MaxConsecutiveCrashes = 0 });

			for (Int32 i = 0; i < 5; i++)
			{
				NativeParseResult crashed = await this.ParseAsync();
				Assert.Equal(NativeParseStatus.Failed, crashed.Status);
			}

			Assert.Equal(5, launcher.Started.Count);
			Assert.False(this._parser!.IsDisabled);
		}

		[Fact]
		public async Task CrashBreaker_ThreeCrashesInARow_TurnTheNativeParserOffForTheSession()
		{
			FakeParserHostLauncher launcher = this.Create(_ => Scripted((host, request) => host.Exit(AccessViolation)));

			NativeParseResult first = await this.ParseAsync();
			NativeParseResult second = await this.ParseAsync();
			NativeParseResult third = await this.ParseAsync();
			NativeParseResult fourth = await this.ParseAsync();
			NativeParseResult fifth = await this.ParseAsync();

			Assert.All(new[] { first, second }, r => Assert.DoesNotContain("turned off", r.Message, StringComparison.Ordinal));
			Assert.Equal(NativeParseStatus.Failed, third.Status);
			Assert.Contains("crashed or hung 3 times in a row", third.Message, StringComparison.Ordinal);
			Assert.Contains("turned off", third.Message, StringComparison.Ordinal);
			Assert.Equal(NativeParseStatus.Unavailable, fourth.Status);
			Assert.Equal(NativeParseStatus.Unavailable, fifth.Status);
			Assert.Contains("built-in parser", fourth.Message, StringComparison.Ordinal);
			Assert.True(this._parser!.IsDisabled);
			Assert.Equal(fourth.Message, this._parser.DisabledReason);
			Assert.Equal(3, launcher.Started.Count);
			Assert.False(Directory.Exists(launcher.WorkingDirectories[2]));
		}

		[Fact]
		public async Task CrashBreaker_AParseThatWorksResetsTheCount()
		{
			Int32 call = 0;
			this.Create(_ => Scripted((host, request) =>
			{
				if (++call == 3)
				{
					host.Respond(request, SampleResult());
				}
				else
				{
					host.Exit(AccessViolation);
				}
			}));

			NativeParseResult[] results =
			{
				await this.ParseAsync(), await this.ParseAsync(), await this.ParseAsync(), await this.ParseAsync(), await this.ParseAsync(),
			};

			Assert.Equal(new[] { NativeParseStatus.Failed, NativeParseStatus.Failed, NativeParseStatus.Completed, NativeParseStatus.Failed, NativeParseStatus.Failed }, results.Select(r => r.Status).ToArray());
			Assert.False(this._parser!.IsDisabled);
		}

		[Fact]
		public async Task CrashBreaker_HangsCountLikeCrashes()
		{
			this.Create(_ => Scripted((host, request) => { }), new NativeParserHostOptions { RequestTimeout = TimeSpan.FromMilliseconds(100), MaxConsecutiveCrashes = 2 });

			await this.ParseAsync();
			NativeParseResult second = await this.ParseAsync();
			NativeParseResult third = await this.ParseAsync();

			Assert.Contains("turned off", second.Message, StringComparison.Ordinal);
			Assert.Equal(NativeParseStatus.Unavailable, third.Status);
		}

		[Fact]
		public async Task CrashBreaker_TheSameTextThatCrashedIsNotSentAgainAndDoesNotCountTwice()
		{
			FakeParserHostLauncher launcher = this.Create(_ => Scripted((host, request) => host.Exit(AccessViolation)));
			const String Crashing = "procedure a(variable w);procedure a(variable w);";

			NativeParseResult first = await this.ParseAsync(default, Crashing);
			NativeParseResult again = await this.ParseAsync(default, Crashing);
			NativeParseResult andAgain = await this.ParseAsync(default, Crashing);

			Assert.Equal(NativeParseStatus.Failed, first.Status);
			Assert.Equal(NativeParseStatus.Failed, again.Status);
			Assert.Contains("exactly this text", again.Message, StringComparison.Ordinal);
			Assert.Equal(NativeParseStatus.Failed, andAgain.Status);
			Assert.False(this._parser!.IsDisabled);
			Assert.Single(launcher.Started);
		}

		[Fact]
		public async Task CrashBreaker_TheSameTextIsParsedAgainWhenTheHostWorksAfterwardsOnOtherText()
		{
			Int32 call = 0;
			FakeParserHostLauncher launcher = this.Create(_ => Scripted((host, request) =>
			{
				if (++call == 1)
				{
					host.Exit(AccessViolation);
				}
				else
				{
					host.Respond(request, SampleResult());
				}
			}));

			NativeParseResult crashed = await this.ParseAsync(default, "crashing text");
			NativeParseResult other = await this.ParseAsync(default, "fixed text");
			NativeParseResult retried = await this.ParseAsync(default, "crashing text");

			Assert.Equal(NativeParseStatus.Failed, crashed.Status);
			Assert.True(other.HasData);
			Assert.Equal(NativeParseStatus.Failed, retried.Status);
			Assert.Contains("exactly this text", retried.Message, StringComparison.Ordinal);
			Assert.Equal(2, launcher.Started.Count);
		}

		[Fact]
		public async Task CrashBreaker_AReplacedParserLibraryGetsAFreshStart()
		{
			FakeParserHostLauncher launcher = this.Create(
				index => index < 2 ? Scripted((host, request) => host.Exit(AccessViolation)) : Normal(index),
				new NativeParserHostOptions { MaxConsecutiveCrashes = 2 });
			await this.ParseAsync();
			await this.ParseAsync();
			NativeParseResult disabled = await this.ParseAsync();
			String library = Path.Combine(this._directory.Path, "resources", "parser.dll");

			File.WriteAllText(library, "a newer parser.dll");
			File.SetLastWriteTimeUtc(library, DateTime.UtcNow.AddMinutes(5));
			NativeParseResult afterUpdate = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Unavailable, disabled.Status);
			Assert.True(afterUpdate.HasData, afterUpdate.Message);
			Assert.False(this._parser!.IsDisabled);
		}

		[Fact]
		public async Task ParseAsync_HostEndsWithoutAnAnswer_ReportsTheErrorOutputOfTheHost()
		{
			this.Create(_ => Scripted((host, request) =>
			{
				host.StandardErrorTail = "Unhandled exception. System.InvalidOperationException: boom";
				host.Exit(unchecked((Int32)0xE0434352));
			}));

			NativeParseResult result = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("unhandled .NET exception", result.Message, StringComparison.Ordinal);
			Assert.Contains("System.InvalidOperationException: boom", result.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_HostHangs_TimesOutKillsTheHostAndRecovers()
		{
			FakeParserHostLauncher launcher = this.Create(
				index => index == 0 ? Scripted((host, request) => { }) : Normal(index),
				new NativeParserHostOptions { RequestTimeout = TimeSpan.FromMilliseconds(200) });

			NativeParseResult hung = await this.ParseAsync();
			NativeParseResult recovered = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, hung.Status);
			Assert.Contains("did not answer within 0.2 seconds", hung.Message, StringComparison.Ordinal);
			Assert.True(launcher.Started[0].Killed);
			Assert.True(launcher.Started[0].Disposed);
			Assert.True(recovered.HasData);
			Assert.Equal(2, launcher.Started.Count);
		}

		[Fact]
		public async Task ParseAsync_HostNeverAnswersThePing_TimesOutAsAStartFailure()
		{
			FakeParserHostLauncher launcher = this.Create(
				_ => new FakeParserHost((host, line) => { }),
				new NativeParserHostOptions { RequestTimeout = TimeSpan.FromMilliseconds(100) });

			NativeParseResult first = await this.ParseAsync();
			NativeParseResult second = await this.ParseAsync();
			NativeParseResult third = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, first.Status);
			Assert.Equal(NativeParseStatus.Failed, second.Status);
			Assert.Equal(NativeParseStatus.Unavailable, third.Status);
			Assert.Equal(2, launcher.Started.Count);
			Assert.All(launcher.Started, h => Assert.True(h.Killed));
		}

		[Fact]
		public async Task ParseAsync_GarbageBeforeTheAnswer_IsSkipped()
		{
			this.Create(_ => Scripted((host, request) =>
			{
				host.Emit("Compiling something...");
				host.Emit(String.Empty);
				host.Emit("{\"looks\":\"like json but is not a response\"}");
				host.Emit("printf noise " + HostProtocol.SerializeResponse(HostMapper.ToResponse(request.Id, SampleResult())));
			}));

			NativeParseResult result = await this.ParseAsync();

			Assert.True(result.HasData, result.Message);
			Assert.Equal("start", Assert.Single(result.Procedures).Name);
		}

		[Fact]
		public async Task ParseAsync_HostPrintsOnlyGarbageAndExits_ReportsAFailureWithTheFirstLine()
		{
			this.Create(_ => Scripted((host, request) =>
			{
				host.Emit("This program cannot be run in DOS mode.");
				host.Exit(3);
			}));

			NativeParseResult result = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("exit code 3", result.Message, StringComparison.Ordinal);
			Assert.Contains("This program cannot be run in DOS mode.", result.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_HostPrintsGarbageForeverWithoutAnAnswer_IsStopped()
		{
			FakeParserHostLauncher launcher = this.Create(_ => Scripted((host, request) =>
			{
				for (Int32 i = 0; i < 20_000; i++)
				{
					host.Emit(new String('x', 100));
				}
			}));

			NativeParseResult result = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("without a response", result.Message, StringComparison.Ordinal);
			Assert.True(launcher.Started[0].Killed);
		}

		[Theory]
		[InlineData("not json at all", "not valid JSON")]
		[InlineData("{\"version\":1,\"id\":", "not valid JSON")]
		[InlineData("null", "not valid JSON")]
		public async Task ParseAsync_InvalidJsonAnswer_FailsAndRestartsTheHost(String json, String expected)
		{
			FakeParserHostLauncher launcher = this.Create(_ => Scripted((host, request) => host.Emit(HostProtocol.ResponsePrefix + json)));

			NativeParseResult result = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("invalid answer", result.Message, StringComparison.Ordinal);
			Assert.Contains(expected, result.Message, StringComparison.Ordinal);
			Assert.True(launcher.Started[0].Killed);
		}

		[Fact]
		public async Task ParseAsync_AnswerToAnotherRequest_IsRejected()
		{
			FakeParserHostLauncher launcher = this.Create(_ => Scripted((host, request) => host.Respond(new HostRequest { Id = request.Id + 10 }, SampleResult())));

			NativeParseResult result = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("answer to request", result.Message, StringComparison.Ordinal);
			Assert.True(launcher.Started[0].Killed);
		}

		[Fact]
		public async Task ParseAsync_AnswerWithUnknownStatusOrVersion_IsRejected()
		{
			Int32 call = 0;
			this.Create(_ => Scripted((host, request) =>
			{
				HostResponse response = HostMapper.ToResponse(request.Id, SampleResult());
				if (call++ == 0)
				{
					response.Status = "Banana";
				}
				else
				{
					response.Version = 99;
				}

				host.Emit(HostProtocol.SerializeResponse(response));
			}));

			NativeParseResult unknownStatus = await this.ParseAsync();
			NativeParseResult unknownVersion = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, unknownStatus.Status);
			Assert.Contains("Unknown status 'Banana'", unknownStatus.Message, StringComparison.Ordinal);
			Assert.Equal(NativeParseStatus.Failed, unknownVersion.Status);
			Assert.Contains("protocol version 99", unknownVersion.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_AnswerWithNullCollections_IsRejectedWithoutThrowing()
		{
			this.Create(_ => Scripted((host, request) => host.Emit(HostProtocol.ResponsePrefix + "{\"version\":1,\"id\":" + request.Id + ",\"status\":\"Completed\",\"exitCode\":0,\"procedures\":null,\"variables\":null}")));

			NativeParseResult result = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("invalid answer", result.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_Cancelled_KillsTheHostThrowsAndTheNextRequestWorks()
		{
			FakeParserHostLauncher launcher = this.Create(index => index == 0 ? Scripted((host, request) => { }) : Normal(index));
			using var cts = new CancellationTokenSource();
			cts.CancelAfter(150);

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => this.ParseAsync(cts.Token));
			NativeParseResult next = await this.ParseAsync();

			Assert.True(launcher.Started[0].Killed);
			Assert.True(launcher.Started[0].Disposed);
			Assert.True(next.HasData);
			Assert.Equal(2, launcher.Started.Count);
		}

		[Fact]
		public async Task ParseAsync_CancelledWhileWaitingForTheTurn_ThrowsAndLeavesTheRunningRequestAlone()
		{
			var release = new TaskCompletionSource();
			FakeParserHostLauncher launcher = this.Create(index => Scripted((host, request) => release.Task.ContinueWith(done => host.Respond(request, SampleResult()), TaskScheduler.Default)));
			Task<NativeParseResult> running = this.ParseAsync();
			await Task.Delay(100);
			using var cts = new CancellationTokenSource();
			Task<NativeParseResult> waiting = this.ParseAsync(cts.Token);
			cts.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
			release.SetResult();
			NativeParseResult result = await running;

			Assert.True(result.HasData);
			Assert.False(launcher.Started[0].Killed);
		}

		[Fact]
		public async Task ParseAsync_HostCannotBeStarted_FailsAndThenBacksOff()
		{
			FakeParserHostLauncher launcher = this.Create(Normal, new NativeParserHostOptions { StartFailureBackoff = TimeSpan.FromSeconds(10) }, new ManualTimeProvider());
			launcher.StartException = new Win32Exception("The system cannot find the file specified.");

			NativeParseResult first = await this.ParseAsync();
			NativeParseResult second = await this.ParseAsync();
			NativeParseResult third = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, first.Status);
			Assert.Contains("could not be started", first.Message, StringComparison.Ordinal);
			Assert.Equal(NativeParseStatus.Failed, second.Status);
			Assert.Equal(NativeParseStatus.Unavailable, third.Status);
			Assert.Contains("keeps failing", third.Message, StringComparison.Ordinal);
			Assert.Contains("The system cannot find the file", third.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_HostDiesBeforeAnsweringThePing_BacksOffThenTriesAgainAfterTheDelay()
		{
			var time = new ManualTimeProvider();
			FakeParserHostLauncher launcher = this.Create(
				_ => new FakeParserHost((host, line) =>
				{
					host.StandardErrorTail = "You must install .NET to run this application.";
					host.Exit(unchecked((Int32)0x80008096));
				}),
				new NativeParserHostOptions { StartFailureBackoff = TimeSpan.FromSeconds(15) },
				time);

			NativeParseResult first = await this.ParseAsync();
			NativeParseResult second = await this.ParseAsync();
			NativeParseResult whileBackedOff = await this.ParseAsync();
			time.Advance(TimeSpan.FromSeconds(16));
			NativeParseResult afterDelay = await this.ParseAsync();
			NativeParseResult backedOffAgain = await this.ParseAsync();

			Assert.Equal(NativeParseStatus.Failed, first.Status);
			Assert.Contains(".NET runtime of the host could not be found", first.Message, StringComparison.Ordinal);
			Assert.Contains("You must install .NET", first.Message, StringComparison.Ordinal);
			Assert.Equal(NativeParseStatus.Failed, second.Status);
			Assert.Equal(NativeParseStatus.Unavailable, whileBackedOff.Status);
			Assert.Equal(NativeParseStatus.Failed, afterDelay.Status);
			Assert.Equal(NativeParseStatus.Unavailable, backedOffAgain.Status);
			Assert.Equal(3, launcher.Started.Count);
		}

		[Fact]
		public async Task ParseAsync_IdleHostDiedBetweenRequests_StartsANewHostTransparently()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);
			await this.ParseAsync();
			launcher.Started[0].Exit(1);

			NativeParseResult result = await this.ParseAsync();

			Assert.True(result.HasData);
			Assert.Equal(2, launcher.Started.Count);
		}

		[Fact]
		public async Task ParseAsync_PipeBrokenOnAReusedHost_RepeatsTheRequestOnceOnAFreshHost()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);
			await this.ParseAsync();
			launcher.Started[0].FailWrites = true;

			NativeParseResult result = await this.ParseAsync();

			Assert.True(result.HasData);
			Assert.Equal(2, launcher.Started.Count);
			Assert.Single(launcher.Started[1].Requests, r => HostProtocol.DeserializeRequest(r).Command == HostProtocol.ParseCommand);
		}

		[Fact]
		public async Task ParseAsync_PipeBrokenOnAFreshHostToo_Fails()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);
			await this.ParseAsync();
			launcher.Started[0].FailWrites = true;
			launcher.Started.Clear();
			FakeParserHostLauncher second = new FakeParserHostLauncher(_ => new FakeParserHost((host, line) => { }) { FailWrites = true });
			using var parser = new OutOfProcessNativeParser(new NativeParserHostOptions { HostPath = this._directory.Write("h2\\host.exe", "x"), WorkingDirectoryRoot = this._directory.Path }, null, second);

			NativeParseResult result = await parser.ParseAsync("x", @"C:\s\a.ssl", this.Options(), CancellationToken.None);

			Assert.Equal(NativeParseStatus.Failed, result.Status);
			Assert.Contains("could not be sent", result.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_AfterTheConfiguredNumberOfRequests_ReplacesTheHost()
		{
			FakeParserHostLauncher launcher = this.Create(Normal, new NativeParserHostOptions { RecycleAfterRequests = 2 });

			await this.ParseAsync();
			await this.ParseAsync();
			Assert.Single(launcher.Started);
			await this.ParseAsync();

			Assert.Equal(2, launcher.Started.Count);
			Assert.True(launcher.Started[0].Disposed);
			Assert.False(launcher.Started[1].Disposed);
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
		public async Task Dispose_StopsTheHostAndLaterRequestsAreUnavailable()
		{
			FakeParserHostLauncher launcher = this.Create(Normal);
			await this.ParseAsync();

			this._parser!.Dispose();
			NativeParseResult after = await this.ParseAsync();

			Assert.True(launcher.Started[0].Killed);
			Assert.True(launcher.Started[0].Disposed);
			Assert.Equal(NativeParseStatus.Unavailable, after.Status);
			this._parser.Dispose();
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
