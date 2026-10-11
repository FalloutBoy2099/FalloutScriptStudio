namespace FalloutScriptStudio.ParserHost
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;

	/// <summary>
	/// Tells <see cref="CrashSilencer"/> when the native library is running, so that a crash inside it kills the host process at once.
	/// </summary>
	internal sealed class NativeCallGuard : INativeParser
	{
		private readonly INativeParser _inner;

		public NativeCallGuard(INativeParser inner)
		{
			this._inner = inner;
		}

		public async Task<NativeParseResult> ParseAsync(String scriptText, String filePath, ParserOptions options, CancellationToken cancellationToken)
		{
			CrashSilencer.EnterNativeRegion();
			try
			{
				return await this._inner.ParseAsync(scriptText, filePath, options, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				CrashSilencer.ExitNativeRegion();
			}
		}
	}
}
