namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	public class ParsingRegistrationTests
	{
		[Fact]
		public async Task AddScriptParsing_RegistersAWorkingParserThatFollowsOptionChanges()
		{
			ParserOptions current = new ParserOptions { EnableNativeParser = false };
			var services = new ServiceCollection();
			services.AddScriptParsing(_ => current);
			using ServiceProvider provider = services.BuildServiceProvider();

			IScriptParser parser = provider.GetRequiredService<IScriptParser>();
			ParseResult off = await parser.ParseAsync(new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\a.ssl" }, CancellationToken.None);
			current = new ParserOptions { EnableNativeParser = true };
			ParseResult on = await parser.ParseAsync(new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\a.ssl" }, CancellationToken.None);

			Assert.Same(parser, provider.GetRequiredService<IScriptParser>());
			Assert.Same(parser.GlobalMacros, provider.GetRequiredService<MacroRegistry>());
			Assert.Equal(ParseOutcome.NativeDisabled, off.Outcome);
			Assert.Equal(ParseOutcome.NativeUnavailable, on.Outcome);
			Assert.NotNull(provider.GetRequiredService<ICollapsedProcedureStore>());
		}
	}
}
