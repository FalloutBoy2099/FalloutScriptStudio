namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Xunit;

	public class NewNodeViewModelTests
	{
		private static NewNodeViewModel Create(FlowchartHarness harness)
		{
			NewNodeViewModel viewModel = new NewNodeViewModel(harness.ScriptEditor, harness.Dialogs);
			viewModel.Initialize(harness.Script);
			return viewModel;
		}

		[Fact]
		public void Starts_WithTheSuggestedNameAndTheEmptyTemplate()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NewNodeViewModel viewModel = Create(harness);

				Assert.Equal("Node###", viewModel.NodeName);
				Assert.Equal("<Empty>", viewModel.SelectedTemplate.Name);
				Assert.Equal(String.Empty, viewModel.Code);
				Assert.Equal(4, viewModel.Templates.Count);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void ChoosingATemplate_FillsTheCode()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NewNodeViewModel viewModel = Create(harness);

				viewModel.SelectedTemplate = viewModel.Templates[1];

				Assert.Contains("NOption(101, Node999, 4);", viewModel.Code, StringComparison.Ordinal);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Create_AddsTheNodeToTheScriptAndCloses()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NewNodeViewModel viewModel = Create(harness);
				Boolean? accepted = null;
				viewModel.CloseRequested += (_, result) => accepted = result;
				viewModel.NodeName = "400";
				viewModel.SelectedTemplate = viewModel.Templates[3];

				await viewModel.CreateAsync();

				Assert.True(accepted);
				Assert.Equal("Node400", viewModel.CreatedName);
				String text = harness.Script.TextDocument.Text;
				Assert.Contains("procedure Node400;", text, StringComparison.Ordinal);
				Assert.Contains("procedure Node400 begin", text, StringComparison.Ordinal);
				Assert.Contains("NMessage(100);", text, StringComparison.Ordinal);
				Assert.NotNull(harness.Script.ProgramInfo!.GetProcedure("Node400"));
			});
		}

		[Fact]
		public void Create_WithTheUnchangedPlaceholder_StaysOpenAndWarns()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NewNodeViewModel viewModel = Create(harness);
				Boolean closed = false;
				viewModel.CloseRequested += (_, _) => closed = true;
				String original = harness.Script.TextDocument.Text;

				await viewModel.CreateAsync();

				Assert.False(closed);
				Assert.Null(viewModel.CreatedName);
				Assert.Single(harness.Dialogs.Warnings);
				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void Create_WithAnExistingName_Warns()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NewNodeViewModel viewModel = Create(harness);
				viewModel.NodeName = "Node001";

				await viewModel.CreateAsync();

				Assert.Contains("already exists", Assert.Single(harness.Dialogs.Warnings), StringComparison.Ordinal);
				Assert.Null(viewModel.CreatedName);
			});
		}

		[Fact]
		public void Cancel_ClosesWithoutAResult()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NewNodeViewModel viewModel = Create(harness);
				Boolean? accepted = null;
				viewModel.CloseRequested += (_, result) => accepted = result;

				viewModel.Cancel();

				Assert.False(accepted);
				Assert.Null(viewModel.CreatedName);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void TheTemplates_AreTheLegacyOnes()
		{
			Assert.Equal(new[] { "<Empty>", "Template Code 1", "Template Code 2", "Template Code 3" }, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(DialogueNodeTemplate.All, t => t.Name)));
		}
	}
}