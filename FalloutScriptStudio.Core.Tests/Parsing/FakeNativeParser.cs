namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;

	/// <summary>A scriptable <see cref="INativeParser"/> used to test the orchestration without parser.dll.</summary>
	internal sealed class FakeNativeParser : INativeParser
	{
		public NativeParseResult Result { get; set; } = new NativeParseResult(NativeParseStatus.Unavailable) { Message = "fake" };

		public List<String> ParsedTexts { get; } = new List<String>();

		public Task<NativeParseResult> ParseAsync(String scriptText, String filePath, ParserOptions options, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			this.ParsedTexts.Add(scriptText);
			return Task.FromResult(this.Result);
		}
	}
}
