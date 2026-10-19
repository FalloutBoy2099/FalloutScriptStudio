namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>A single-thread synchronization context like the UI thread: everything posted to it runs on its own thread.</summary>
	internal sealed class PumpSynchronizationContext : SynchronizationContext, IDisposable
	{
		private readonly BlockingCollection<(SendOrPostCallback Callback, Object? State)> _queue = [];
		private readonly Thread _thread;

		public PumpSynchronizationContext()
		{
			this._thread = new Thread(this.Run) { IsBackground = true };
			this._thread.Start();
		}

		public override void Post(SendOrPostCallback d, Object? state)
		{
			this._queue.Add((d, state));
		}

		/// <summary>Runs an asynchronous function on the pump thread and completes with its result.</summary>
		public Task<T> RunAsync<T>(Func<Task<T>> function)
		{
			TaskCompletionSource<T> completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
			this.Post(_ => _ = Execute(function, completion), null);
			return completion.Task;
		}

		public void Dispose()
		{
			this._queue.CompleteAdding();
		}

		private static async Task Execute<T>(Func<Task<T>> function, TaskCompletionSource<T> completion)
		{
			try
			{
				completion.SetResult(await function());
			}
			catch (Exception ex)
			{
				completion.SetException(ex);
			}
		}

		private void Run()
		{
			SetSynchronizationContext(this);
			foreach ((SendOrPostCallback callback, Object? state) in this._queue.GetConsumingEnumerable())
			{
				callback(state);
			}
		}
	}
}