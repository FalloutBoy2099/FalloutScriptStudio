namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using Xunit;

	/// <summary>Host crashes and the crash breaker of <see cref="OutOfProcessNativeParser"/>.</summary>
	[Collection("Test collection for FalloutScriptStudio.Core.Tests.Native.OutOfProcessNativeParserTests")]
	public sealed class OutOfProcessNativeParserCrashTests : OutOfProcessNativeParserTestBase
	{
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
	}
}
