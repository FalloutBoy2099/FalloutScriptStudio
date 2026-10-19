namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using Xunit;

	/// <summary>Garbage and invalid answers of the host of <see cref="OutOfProcessNativeParser"/>.</summary>
	[Collection("Test collection for FalloutScriptStudio.Core.Tests.Native.OutOfProcessNativeParserTests")]
	public sealed class OutOfProcessNativeParserAnswerTests : OutOfProcessNativeParserTestBase
	{
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
	}
}