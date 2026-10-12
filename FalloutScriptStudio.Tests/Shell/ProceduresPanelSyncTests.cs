namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Panels;
	using Xunit;

	/// <summary>
	/// The procedures tree is updated in place after a parse: WPF creates a container for every visible node of a tree that is built
	/// again (about a millisecond each), and a tree that is updated in place keeps its containers, the expansion and the selection.
	/// </summary>
	public sealed class ProceduresPanelSyncTests
	{
		private const String Script =
			"procedure start;\r\n" +
			"procedure helper;\r\n" +
			"procedure talk_p_proc;\r\n" +
			"\r\n" +
			"procedure start begin\r\n" +
			"   call helper;\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure helper begin\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure talk_p_proc begin\r\n" +
			"end\r\n";

		private static async Task<(ShellHarness Harness, DocumentViewModel Document)> CreateAsync()
		{
			ShellHarness harness = new ShellHarness();
			DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
			await harness.Workspace.RequestParseAsync(document, true);
			return (harness, document);
		}

		private static TreeGroupNodeViewModel Local(ShellHarness harness)
		{
			return harness.Procedures.Groups.Single(group => group.IsLocal);
		}

		private static ProcedureNodeViewModel Node(ShellHarness harness, String name)
		{
			return Local(harness).Children.OfType<ProcedureNodeViewModel>().Single(node => node.Procedure.Name == name);
		}

		[Fact]
		public void AParseAfterAnEdit_KeepsTheNodesAndUpdatesTheirProcedures()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				TreeGroupNodeViewModel local = Local(harness);
				ProcedureNodeViewModel start = Node(harness, "start");
				ProcedureNodeViewModel helper = Node(harness, "helper");
				Int32 line = helper.Procedure.StartLine;
				List<String> changes = new List<String>();
				helper.PropertyChanged += (_, e) => changes.Add(e.PropertyName ?? String.Empty);

				document.TextDocument.Insert(0, "// a\r\n// b\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.Same(local, Local(harness));
				Assert.Same(start, Node(harness, "start"));
				Assert.Same(helper, Node(harness, "helper"));
				Assert.Equal(line + 2, helper.Procedure.StartLine);
				Assert.Contains(nameof(ProcedureNodeViewModel.Procedure), changes);
				Assert.Equal(new[] { "start", "helper", "talk_p_proc" }, local.Children.Select(child => child.Title));
			});
		}

		[Fact]
		public void NewAndRemovedProcedures_AreAddedAndRemovedInPlace()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				ProcedureNodeViewModel start = Node(harness, "start");
				ProcedureNodeViewModel talk = Node(harness, "talk_p_proc");

				document.TextDocument.Insert(document.TextDocument.TextLength, "\r\nprocedure extra begin\r\nend\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.Equal(new[] { "start", "helper", "talk_p_proc", "extra" }, Local(harness).Children.Select(child => child.Title));
				Assert.Same(start, Node(harness, "start"));
				Assert.Same(talk, Node(harness, "talk_p_proc"));

				String text = document.TextDocument.Text;
				Int32 from = text.IndexOf("procedure helper begin", StringComparison.Ordinal);
				Int32 to = text.IndexOf("procedure talk_p_proc begin", StringComparison.Ordinal);
				document.TextDocument.Remove(from, to - from);
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.DoesNotContain(Local(harness).Children, child => child.Title == "helper");
				Assert.Same(start, Node(harness, "start"));
				Assert.Same(talk, Node(harness, "talk_p_proc"));
			});
		}

		[Fact]
		public void MovedProcedures_KeepTheirNodes_InTheOrderOfTheScript()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				ProcedureNodeViewModel helper = Node(harness, "helper");
				ProcedureNodeViewModel start = Node(harness, "start");

				await harness.Procedures.MoveUpAsync(helper);

				Assert.Equal(new[] { "helper", "start", "talk_p_proc" }, Local(harness).Children.Select(child => child.Title));
				Assert.Same(helper, Node(harness, "helper"));
				Assert.Same(start, Node(harness, "start"));
				Assert.True(helper.Procedure.StartLine < start.Procedure.StartLine);
			});
		}

		[Fact]
		public void TheExpansionAndTheSelectionStayOnTheSameNodes()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				ProcedureNodeViewModel start = Node(harness, "start");
				start.IsExpanded = true;
				ProcedureNodeViewModel helper = Node(harness, "helper");
				harness.Procedures.SelectedNode = helper;
				helper.IsSelected = true;

				document.TextDocument.Insert(0, "// a\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.True(start.IsExpanded);
				Assert.Same(helper, harness.Procedures.SelectedNode);
				Assert.True(helper.IsSelected);
			});
		}

		[Fact]
		public void TheSelectionIsDroppedWhenTheSelectedProcedureDisappears()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Procedures.SelectedNode = Node(harness, "helper");

				String text = document.TextDocument.Text;
				Int32 from = text.IndexOf("procedure helper begin", StringComparison.Ordinal);
				Int32 to = text.IndexOf("procedure talk_p_proc begin", StringComparison.Ordinal);
				document.TextDocument.Remove(from, to - from);
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.Null(harness.Procedures.SelectedNode);
			});
		}

		[Fact]
		public void EachDocumentGetsItsOwnTree()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel first) = await CreateAsync();
				using ShellHarness _h = harness;
				ProcedureNodeViewModel firstStart = Node(harness, "start");
				DocumentViewModel second = await harness.OpenFileAsync("b.ssl", Script.Replace("helper", "other", StringComparison.Ordinal));
				await harness.Workspace.RequestParseAsync(second, true);

				Assert.NotSame(firstStart, Node(harness, "start"));
				Assert.Equal(new[] { "start", "other", "talk_p_proc" }, Local(harness).Children.Select(child => child.Title));

				harness.Workspace.ActiveDocument = first;

				Assert.Equal(new[] { "start", "helper", "talk_p_proc" }, Local(harness).Children.Select(child => child.Title));
			});
		}

		[Fact]
		public void TheGroupKnowsWhenItBecomesEmptyAndFilledAgain()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				TreeGroupNodeViewModel local = Local(harness);
				List<String> changes = new List<String>();
				local.PropertyChanged += (_, e) => changes.Add(e.PropertyName ?? String.Empty);

				document.TextDocument.Text = "// nothing\r\n";
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.Same(local, Local(harness));
				Assert.True(local.IsEmpty);
				Assert.Contains(nameof(TreeGroupNodeViewModel.IsEmpty), changes);

				document.TextDocument.Text = Script;
				await harness.Workspace.RequestParseAsync(document, true);
				Assert.False(local.IsEmpty);
				Assert.Equal(3, local.Children.Count);
			});
		}
	}
}
