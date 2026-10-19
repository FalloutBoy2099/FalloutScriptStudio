namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Startup;

	/// <summary>A single-instance lock whose answers the test chooses.</summary>
	internal sealed class FakeSingleInstance : ISingleInstance
	{
		private readonly StartupEvents _events;
		private readonly Queue<Boolean> _acquireResults;

		public FakeSingleInstance(StartupEvents events, params Boolean[] acquireResults)
		{
			this._events = events;
			this._acquireResults = new Queue<Boolean>(acquireResults);
		}

		public event EventHandler<String[]>? ArgumentsReceived;

		public Boolean SendResult { get; set; } = true;

		public String[]? Sent { get; private set; }

		public Boolean Listening { get; private set; }

		public Int32 DisposeCount { get; private set; }

		public Boolean TryAcquire()
		{
			this._events.Add("single.TryAcquire");
			return this._acquireResults.Count > 0 && this._acquireResults.Dequeue();
		}

		public void StartListening()
		{
			this._events.Add("single.StartListening");
			this.Listening = true;
		}

		public Task<Boolean> SendArgumentsAsync(String[] arguments, CancellationToken cancellationToken = default)
		{
			this._events.Add("single.Send");
			this.Sent = arguments;
			return Task.FromResult(this.SendResult);
		}

		public void Raise(String[] arguments)
		{
			this.ArgumentsReceived?.Invoke(this, arguments);
		}

		public void Dispose()
		{
			this._events.Add("single.Dispose");
			this.DisposeCount++;
		}
	}
}