namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.Tests.Functions;
	using FalloutScriptStudio.Core.TextTools;
	using Microsoft.Extensions.DependencyInjection;
	using Xunit;

	public class ServiceRegistrationTests : IDisposable
	{
		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		private ServiceProvider BuildProvider(Boolean withParsing)
		{
			var services = new ServiceCollection();
			if (withParsing)
			{
				services.AddScriptParsing(_ => new ParserOptions { EnableNativeParser = false });
			}

			services.AddTextTools();
			services.AddRefactoring();
			services.AddDialogueTools(_ => this._folder.Combine("DialogFunc.cfg"));
			services.AddFunctionCatalog(
				_ => new FunctionCatalogPaths(RepositoryFiles.Description("functions.ini"), this._folder.Combine("userfunctions.ini")),
				_ => this._folder.Combine("user-keywords.json"));
			return services.BuildServiceProvider();
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void EveryService_CanBeResolved(Boolean withParsing)
		{
			using ServiceProvider provider = this.BuildProvider(withParsing);

			Assert.IsType<CodeFormatter>(provider.GetRequiredService<ICodeFormatter>());
			Assert.IsType<TextSearcher>(provider.GetRequiredService<ITextSearcher>());
			Assert.IsType<ProcedureTextService>(provider.GetRequiredService<IProcedureTextService>());
			Assert.NotNull(provider.GetRequiredService<KeywordExpander>());
			Assert.NotNull(provider.GetRequiredService<IncludeInserter>());
			Assert.IsType<RefactoringService>(provider.GetRequiredService<IRefactoringService>());
			Assert.IsType<DialogueNodeParser>(provider.GetRequiredService<IDialogueNodeParser>());
			Assert.IsType<FlowchartBuilder>(provider.GetRequiredService<IFlowchartBuilder>());
			Assert.IsType<FlowchartDocumentStore>(provider.GetRequiredService<IFlowchartDocumentStore>());
			Assert.NotNull(provider.GetRequiredService<DialogueNodeFactory>());
			Assert.NotNull(provider.GetRequiredService<DialogueNodeEditor>());
			Assert.IsType<UserKeywordStore>(provider.GetRequiredService<IUserKeywordStore>());
			Assert.IsType<FunctionCatalogService>(provider.GetRequiredService<IFunctionCatalogService>());
		}

		[Fact]
		public void RulesService_IsTheSourceOfTheParser_AndIsShared()
		{
			using ServiceProvider provider = this.BuildProvider(true);

			Assert.Same(provider.GetRequiredService<IDialogueRulesService>(), provider.GetRequiredService<IDialogueRulesSource>());
			Assert.Same(provider.GetRequiredService<IProcedureTextService>(), provider.GetRequiredService<IProcedureTextService>());
		}

		[Fact]
		public async Task DisposingTheProvider_DisposesTheServicesThatOwnALock()
		{
			ServiceProvider provider = this.BuildProvider(false);
			IDialogueRulesService rules = provider.GetRequiredService<IDialogueRulesService>();
			NodeCodeSnippetStore snippets = new NodeCodeSnippetStore(this._folder.Combine("snippets.cfg"));
			IUserKeywordStore keywords = provider.GetRequiredService<IUserKeywordStore>();
			IFunctionCatalogService catalog = provider.GetRequiredService<IFunctionCatalogService>();
			Assert.IsAssignableFrom<IDisposable>(rules);
			Assert.IsAssignableFrom<IDisposable>(snippets);
			Assert.IsAssignableFrom<IDisposable>(keywords);
			Assert.IsAssignableFrom<IDisposable>(catalog);

			provider.Dispose();
			snippets.Dispose();

			await Assert.ThrowsAsync<ObjectDisposedException>(() => rules.LoadAsync());
			await Assert.ThrowsAsync<ObjectDisposedException>(() => snippets.LoadAsync());
			await Assert.ThrowsAsync<ObjectDisposedException>(() => keywords.LoadAsync());
			await Assert.ThrowsAsync<ObjectDisposedException>(() => catalog.LoadAsync());
		}

		[Fact]
		public void DialoguePreview_IsTransient()
		{
			using ServiceProvider provider = this.BuildProvider(false);

			Assert.NotSame(provider.GetRequiredService<DialoguePreview>(), provider.GetRequiredService<DialoguePreview>());
		}
	}
}
