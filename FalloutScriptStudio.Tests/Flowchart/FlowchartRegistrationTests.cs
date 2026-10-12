namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.DialoguePreview;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public class FlowchartRegistrationTests : IDisposable
	{
		private readonly ScriptToolsTempDirectory _folder = new ScriptToolsTempDirectory();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		private ServiceProvider CreateProvider()
		{
			ServiceCollection services = new ServiceCollection();
			FakeSettingsService settings = new FakeSettingsService(this._folder.Path);
			(IWorkspace workspace, TestWorkspace _) = TestWorkspace.Create();
			(IScriptToolService tools, ScriptToolsStub _) = ScriptToolsStub.Create();
			services.AddSingleton<ISettingsService>(settings);
			services.AddSingleton(workspace);
			services.AddSingleton(tools);
			services.AddSingleton<IDialogService>(new FakeDialogs());
			services.AddSingleton<IThemeService>(new FixedThemeService(false));
			services.AddSingleton<IMessageEditorService>(new FakeMessageEditorService());
			services.AddSingleton<IMessageFileService>(new MessageFileService(NullLogger<MessageFileService>.Instance));
			services.AddSingleton<ICollapsedProcedureStore, InMemoryCollapsedProcedureStore>();
			services.AddSingleton<ScriptEditorServices>();
			services.AddScriptParsing(_ => new ParserOptions());
			services.AddTextTools();
			services.AddDialogueTools(_ => this._folder.Combine("DialogFunc.cfg"), _ => this._folder.Combine("CustomCode.ini"));
			services.AddFlowchartTools();
			return services.BuildServiceProvider();
		}

		[Fact]
		public void Services_AreResolvable()
		{
			using ServiceProvider provider = this.CreateProvider();

			Assert.IsType<DialogueMessageService>(provider.GetRequiredService<IDialogueMessageService>());
			Assert.IsType<NodeCodeEditorService>(provider.GetRequiredService<INodeCodeEditorService>());
			Assert.IsType<FlowchartDialogService>(provider.GetRequiredService<IFlowchartDialogService>());
			Assert.Same(provider.GetRequiredService<DialogueScriptEditor>(), provider.GetRequiredService<DialogueScriptEditor>());
		}

		[Fact]
		public void ViewModels_AreTransient()
		{
			StaTestRunner.Run(() =>
			{
				using ServiceProvider provider = this.CreateProvider();

				Assert.NotSame(provider.GetRequiredService<FlowchartViewModel>(), provider.GetRequiredService<FlowchartViewModel>());
				Assert.NotNull(provider.GetRequiredService<NodeCodeEditorViewModel>());
				Assert.NotNull(provider.GetRequiredService<NewNodeViewModel>());
				Assert.NotNull(provider.GetRequiredService<MessageTextViewModel>());
				DialoguePreviewViewModel first = provider.GetRequiredService<DialoguePreviewViewModel>();
				Assert.NotSame(first, provider.GetRequiredService<DialoguePreviewViewModel>());
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void NodeCodeEditor_WorksWithoutASnippetStore()
		{
			StaTestRunner.Run(() =>
			{
				ServiceCollection services = new ServiceCollection();
				(IWorkspace workspace, TestWorkspace _) = TestWorkspace.Create();
				services.AddSingleton(workspace);
				services.AddSingleton<IDialogService>(new FakeDialogs());
				services.AddSingleton<IMessageEditorService>(new FakeMessageEditorService());
				services.AddSingleton<IDialogueMessageService>(new FakeDialogueMessageService());
				services.AddScriptParsing(_ => new ParserOptions());
				services.AddTextTools();
				services.AddDialogueTools(_ => this._folder.Combine("DialogFunc.cfg"));
				services.AddSingleton<DialogueScriptEditor>();
				services.AddTransient<NodeCodeEditorViewModel>();
				using ServiceProvider provider = services.BuildServiceProvider();

				Assert.False(provider.GetRequiredService<NodeCodeEditorViewModel>().CanStoreSnippets);
				return Task.CompletedTask;
			});
		}
	}
}
