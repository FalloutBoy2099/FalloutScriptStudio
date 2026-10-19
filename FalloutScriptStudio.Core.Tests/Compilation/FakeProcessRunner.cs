namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// An <see cref="IProcessRunner"/> that records the requests and lets a test script the outcome (including file side effects).
	/// </summary>
	internal sealed class FakeProcessRunner : IProcessRunner
	{
		private readonly ConcurrentQueue<ProcessRunRequest> _requests = new ConcurrentQueue<ProcessRunRequest>();

		/// <summary>Gets or sets the function that produces the result of a run.</summary>
		public Func<ProcessRunRequest, ProcessRunResult> Handler { get; set; } = _ => new ProcessRunResult(0, String.Empty);

		/// <summary>Gets the requests in the order they were received.</summary>
		public IReadOnlyList<ProcessRunRequest> Requests => this._requests.ToArray();

		/// <inheritdoc/>
		public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken = default)
		{
			this._requests.Enqueue(request);
			cancellationToken.ThrowIfCancellationRequested();
			return Task.FromResult(this.Handler(request));
		}
	}
}