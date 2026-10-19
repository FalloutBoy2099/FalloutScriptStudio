namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class SingleInstanceServiceTests : IDisposable
	{
		private readonly TestFolder _folder = new TestFolder("instance");
		private readonly String _installation;
		private SingleInstanceService? _primary;
		private SingleInstanceService? _secondary;

		public SingleInstanceServiceTests()
		{
			this._installation = this._folder.Path;
		}

		public void Dispose()
		{
			this._secondary?.Dispose();
			this._primary?.Dispose();
			this._folder.Dispose();
		}

		private static async Task<T> WithinAsync<T>(Task<T> task, TimeSpan timeout)
		{
			Task finished = await Task.WhenAny(task, Task.Delay(timeout));
			Assert.Same(task, finished);
			return await task;
		}

		[Fact]
		public async Task SendArguments_WaitsForAFirstInstanceThatIsStillStarting()
		{
			this._primary = new SingleInstanceService(this._installation);
			Assert.True(this._primary.TryAcquire());
			TaskCompletionSource<String[]> received = new TaskCompletionSource<String[]>(TaskCreationOptions.RunContinuationsAsynchronously);
			this._primary.ArgumentsReceived += (_, arguments) => received.TrySetResult(arguments);
			this._secondary = new SingleInstanceService(this._installation, TimeSpan.FromSeconds(90));
			String script = Path.Combine(this._installation, "late.ssl");

			// The first instance holds the lock but its window (and so its pipe) comes much later than the old 3 s of waiting.
			Task<Boolean> sending = this._secondary.SendArgumentsAsync([script]);
			await Task.Delay(TimeSpan.FromSeconds(3.5));
			this._primary.StartListening();

			Assert.True(await WithinAsync(sending, TimeSpan.FromSeconds(120)));
			Assert.Equal([script], await WithinAsync(received.Task, TimeSpan.FromSeconds(60)));
		}

		[Fact]
		public async Task SendArguments_ReportsFailureWhenNobodyListens()
		{
			this._secondary = new SingleInstanceService(this._installation, TimeSpan.FromMilliseconds(400));
			Stopwatch watch = Stopwatch.StartNew();

			Boolean sent = await this._secondary.SendArgumentsAsync(["x.ssl"]);

			Assert.False(sent);
			Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), "The wait must follow the timeout that was asked for.");
		}

		[Fact]
		public async Task Listener_SurvivesAHandlerThatThrows()
		{
			this._primary = new SingleInstanceService(this._installation);
			Assert.True(this._primary.TryAcquire());
			TaskCompletionSource<String[]> second = new TaskCompletionSource<String[]>(TaskCreationOptions.RunContinuationsAsynchronously);
			Int32 calls = 0;
			this._primary.ArgumentsReceived += (_, arguments) =>
			{
				if (calls++ == 0)
				{
					throw new InvalidOperationException("the handler is broken");
				}

				second.TrySetResult(arguments);
			};
			this._primary.StartListening();
			this._secondary = new SingleInstanceService(this._installation, TimeSpan.FromSeconds(60));

			Assert.True(await this._secondary.SendArgumentsAsync([Path.Combine(this._installation, "one.ssl")]));
			Assert.True(await this._secondary.SendArgumentsAsync([Path.Combine(this._installation, "two.ssl")]));

			String[] arguments = await WithinAsync(second.Task, TimeSpan.FromSeconds(60));
			Assert.EndsWith("two.ssl", Assert.Single(arguments), StringComparison.Ordinal);
		}
	}
}