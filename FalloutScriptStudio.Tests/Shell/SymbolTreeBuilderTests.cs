namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.ViewModels.Panels;
	using Xunit;

	public sealed class SymbolTreeBuilderTests
	{
		private static ProgramInfo CreateInfo(Boolean nativeData)
		{
			ScriptProcedure local = new ScriptProcedure("talk_p_proc")
			{
				DeclaredFile = @"C:\s\npc.ssl",
				StartFile = @"C:\s\npc.ssl",
				FileName = "npc.ssl",
				DeclaredLine = 3,
				StartLine = 10,
				EndLine = 20,
				ArgumentCount = 1,
				Variables = new[] { new ScriptVariable("arg", VariableScope.Local) { ArgumentDeclaredLine = 10, DeclaredLine = 10 } },
			};
			ScriptProcedure other = new ScriptProcedure("zeta")
			{
				DeclaredFile = @"C:\s\npc.ssl",
				StartFile = @"C:\s\npc.ssl",
				FileName = "npc.ssl",
				DeclaredLine = 4,
				StartLine = 5,
				EndLine = 8,
			};
			ScriptProcedure global = new ScriptProcedure("util_proc")
			{
				DeclaredFile = @"C:\h\util.h",
				StartFile = @"C:\h\util.h",
				FileName = "util.h",
				DeclaredLine = 1,
				StartLine = 2,
				EndLine = 4,
			};
			ScriptProcedure imported = new ScriptProcedure("far_proc")
			{
				Flags = ProcedureFlags.Import,
				DeclaredFile = @"C:\s\npc.ssl",
				FileName = "npc.ssl",
				DeclaredLine = 2,
			};
			ScriptProcedure broken = new ScriptProcedure("broken") { FileName = "npc.ssl", StartLine = -1, EndLine = -1 };
			ScriptVariable scriptVariable = new ScriptVariable("counter", VariableScope.Global) { FileName = "npc.ssl", DeclaredLine = 2 };
			ScriptVariable headerVariable = new ScriptVariable("global_flag", VariableScope.Global) { FileName = "defs.h", DeclaredLine = 7 };

			ProgramInfo info = new ProgramInfo(new[] { local, other, global, imported, broken }, new[] { scriptVariable, headerVariable });
			return info.WithFlags(isParsed: true, hasNativeData: nativeData);
		}

		[Fact]
		public void Procedures_are_split_into_global_and_local_groups()
		{
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildProcedureGroups(CreateInfo(true), "NPC.ssl", false);

			Assert.Equal(2, groups.Count);
			Assert.Equal(SymbolTreeBuilder.GlobalProceduresKey, groups[0].Key);
			Assert.Equal(SymbolTreeBuilder.LocalProceduresKey, groups[1].Key);
			Assert.Equal(new[] { "util_proc", "far_proc" }.OrderBy(n => n), groups[0].Children.Select(c => c.Title).OrderBy(n => n));
			Assert.False(groups[0].IsLocal);
			Assert.True(groups[1].IsLocal);
		}

		[Fact]
		public void Local_procedures_are_sorted_by_their_position_in_the_file()
		{
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildProcedureGroups(CreateInfo(true), "npc.ssl", false);

			Assert.Equal(new[] { "zeta", "talk_p_proc" }, groups[1].Children.Select(c => c.Title).Where(t => t is "zeta" or "talk_p_proc"));
		}

		[Fact]
		public void Local_procedure_nodes_can_be_modified_and_global_ones_cannot()
		{
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildProcedureGroups(CreateInfo(true), "npc.ssl", false);

			Assert.All(groups[1].Children, node => Assert.True(node.CanModify));
			Assert.All(groups[0].Children, node => Assert.False(node.CanModify));
			Assert.False(groups[1].CanModify);
		}

		[Fact]
		public void Standard_handlers_are_marked()
		{
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildProcedureGroups(CreateInfo(true), "npc.ssl", false);

			ProcedureNodeViewModel talk = groups[1].Children.OfType<ProcedureNodeViewModel>().Single(n => n.Title == "talk_p_proc");
			ProcedureNodeViewModel zeta = groups[1].Children.OfType<ProcedureNodeViewModel>().Single(n => n.Title == "zeta");
			Assert.True(talk.IsStandard);
			Assert.False(zeta.IsStandard);
		}

		[Fact]
		public void Procedure_variables_are_children_of_the_procedure_node()
		{
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildProcedureGroups(CreateInfo(true), "npc.ssl", false);

			ProcedureNodeViewModel talk = groups[1].Children.OfType<ProcedureNodeViewModel>().Single(n => n.Title == "talk_p_proc");
			VariableNodeViewModel variable = Assert.IsType<VariableNodeViewModel>(Assert.Single(talk.Children));
			Assert.Equal("arg", variable.Title);
		}

		[Fact]
		public void Showing_arguments_changes_the_titles_but_not_the_keys()
		{
			ProgramInfo info = CreateInfo(true);
			TreeGroupNodeViewModel plain = SymbolTreeBuilder.BuildProcedureGroups(info, "npc.ssl", false)[1];
			TreeGroupNodeViewModel detailed = SymbolTreeBuilder.BuildProcedureGroups(info, "npc.ssl", true)[1];

			TreeNodeViewModel a = plain.Children.Single(n => n.Title == "talk_p_proc");
			TreeNodeViewModel b = detailed.Children.Single(n => n.Key == a.Key);
			Assert.Equal("talk_p_proc (arg)", b.Title);
		}

		[Fact]
		public void Without_native_data_only_the_local_group_is_built_and_broken_procedures_are_skipped()
		{
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildProcedureGroups(CreateInfo(false), "npc.ssl", false);

			TreeGroupNodeViewModel local = Assert.Single(groups);
			Assert.True(local.IsLocal);
			Assert.DoesNotContain(local.Children, node => node.Title == "broken");
		}

		[Fact]
		public void Variables_are_split_into_global_and_script_groups()
		{
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildVariableGroups(CreateInfo(true), "NPC.ssl");

			Assert.Equal(2, groups.Count);
			Assert.Equal("global_flag", Assert.Single(groups[0].Children).Title);
			Assert.Equal("counter", Assert.Single(groups[1].Children).Title);
		}

		[Fact]
		public void Variables_need_native_parser_data()
		{
			Assert.Empty(SymbolTreeBuilder.BuildVariableGroups(CreateInfo(false), "npc.ssl"));
		}

		[Fact]
		public void Empty_groups_report_that_they_are_empty()
		{
			TreeGroupNodeViewModel group = SymbolTreeBuilder.BuildProcedureGroups(new ProgramInfo(Array.Empty<ScriptProcedure>(), Array.Empty<ScriptVariable>()).WithFlags(hasNativeData: true), "a.ssl", false)[0];

			Assert.True(group.IsEmpty);
		}
	}
}
