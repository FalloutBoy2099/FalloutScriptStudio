namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using Xunit;

	/// <summary>Timeouts, cancellation, back-off and host replacement of <see cref="OutOfProcessNativeParser"/>.</summary>
	[Collection("Test collection for FalloutScriptStudio.Core.Tests.Native.OutOfProcessNativeParserTests")]
	public sealed class OutOfProcessNativeParserLifecycleTests : OutOfProcessNativeParserTestBase
	{
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
		public async Task ParseAsync_RemovesTheWorkingFoldersOfHostsThatEndedLongAgo_AndKeepsTheRecentOnes()
		{
			String hostsFolder = Path.Combine(this._directory.Path, "FalloutScriptStudio");
			String stale = Path.Combine(hostsFolder, "parser-host-" + Guid.NewGuid().ToString("N"));
			String recent = Path.Combine(hostsFolder, "parser-host-" + Guid.NewGuid().ToString("N"));
			String other = Path.Combine(hostsFolder, "something-else");
			foreach (String directory in new[] { stale, recent, other })
			{
				Directory.CreateDirectory(directory);
			}

			Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-3));
			Directory.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddHours(-3));
			this.Create(Normal);

			await this.ParseAsync();

			Assert.False(Directory.Exists(stale), "the folder of a host that ended long ago must be removed");
			Assert.True(Directory.Exists(recent), "the folder of a recent host may belong to a running instance");
			Assert.True(Directory.Exists(other), "only the working folders of hosts are touched");
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
	}
}