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

		public MacroRegistry GlobalMacros => this._inner.GlobalMacros;

		public TimeSpan Delay { get; set; } = TimeSpan.Zero;

		public Int32 Started => Volatile.Read(ref this._started);

		public Int32 Completed => Volatile.Read(ref this._completed);

		public List<String> Texts { get; } = new List<String>();

		public Int32 MacroReloads { get; private set; }

		public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref this._started);
			lock (this.Texts)
			{
				this.Texts.Add(request.ScriptText);
			}

			if (this.Delay > TimeSpan.Zero)
			{
				await Task.Delay(this.Delay, cancellationToken);
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
