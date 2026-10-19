namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;

	/// <summary>
	/// A script parser for tests: it runs the real internal parser (no native library) after an optional delay and records every request.
	/// </summary>
	internal sealed class FakeParser : IScriptParser
	{
		private readonly ScriptParser _inner = new ScriptParser(new NativeParser(), () => new ParserOptions { EnableNativeParser = false });
		private Int32 _started;
		private Int32 _completed;
		private readonly TaskCompletionSource _firstStart = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public MacroRegistry GlobalMacros => this._inner.GlobalMacros;

		/// <summary>
		/// Gets or sets a task every parse waits for after it started (the wait ends when the parse is cancelled): the test decides when the parses
		/// may go on, instead of guessing it with a delay.
		/// </summary>
		public Task? Gate { get; set; }

		/// <summary>Waits until the first parse has started (at most 30 seconds).</summary>
		/// <returns>A task that completes when a parse started.</returns>
		public Task WaitForStartAsync()
		{
			return this._firstStart.Task.WaitAsync(TimeSpan.FromSeconds(30));
		}

		public Int32 Started => Volatile.Read(ref this._started);

		public Int32 Completed => Volatile.Read(ref this._completed);

		public List<String> Texts { get; } = [];

		public Int32 MacroReloads { get; private set; }

		public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref this._started);
			lock (this.Texts)
			{
				this.Texts.Add(request.ScriptText);
			}

			_ = this._firstStart.TrySetResult();
			if (this.Gate is { } gate)
			{
				await gate.WaitAsync(cancellationToken);
			}

			ParseResult result = await this._inner.ParseAsync(request, cancellationToken);
			Interlocked.Increment(ref this._completed);
			return result;
		}

		public Task ReloadGlobalMacrosAsync(CancellationToken cancellationToken)
		{
			this.MacroReloads++;
			return Task.CompletedTask;
		}
	}
}