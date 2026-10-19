namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Tests.Functions;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public class ProcedureHandlerCatalogTests
	{
		[Fact]
		public void Parse_BuildsGroupsHandlersAndSeparators()
		{
			String[] lines =
			{
				"// comment",
				"start<d>Called first",
				"<m>Objects<d>Object handlers",
				"look_at_p_proc<d>Look",
				"use_p_proc<d>Use",
				"<m>Critters",
				"talk_p_proc<d>Talk",
				"<-m>",
				"<->",
				"<m>Unclosed",
				"Node000<d>Node",
			};

			ProcedureHandlerCatalog catalog = ProcedureHandlerCatalog.Parse(lines);

			Assert.Equal(
				new[] { ProcedureHandlerKind.Handler, ProcedureHandlerKind.Group, ProcedureHandlerKind.Group, ProcedureHandlerKind.Separator, ProcedureHandlerKind.Group },
				catalog.Entries.Select(e => e.Kind).ToArray());
			ProcedureHandlerEntry objects = catalog.Entries[1];
			Assert.Equal("Objects", objects.Name);
			Assert.Equal("Object handlers", objects.Description);
			Assert.Equal(new[] { "look_at_p_proc", "use_p_proc" }, objects.Children.Select(c => c.Name).ToArray());
			Assert.Equal(new[] { "start", "look_at_p_proc", "use_p_proc", "talk_p_proc", "Node000" }, catalog.Handlers.Select(h => h.Name).ToArray());
			Assert.Equal("Talk", catalog.FindDescription("talk_p_proc"));
			Assert.Null(catalog.FindDescription("missing"));
		}

		[Theory]
		[InlineData("look_at_p_proc", true)]
		[InlineData("description_p_proc", true)]
		[InlineData("talk_p_proc", false)]
		[InlineData("Look_At_P_Proc", false)]
		public void RequiresScriptOverrides(String name, Boolean expected)
		{
			Assert.Equal(expected, ProcedureHandlerCatalog.RequiresScriptOverrides(name));
		}

		[Fact]
		public async Task LoadAsync_ReadsTheRealHandlerFile()
		{
			ProcedureHandlerCatalog catalog = await ProcedureHandlerCatalog.LoadAsync(RepositoryFiles.Description("prochandlers.ini"));

			String[] names = catalog.Handlers.Select(h => h.Name).ToArray();
			Assert.Contains("talk_p_proc", names);
			Assert.Contains("map_enter_p_proc", names);
			Assert.Contains("Node999", names);
			Assert.Contains("Example(arg1, arg2)", names);
			Assert.Contains(catalog.Entries, e => e.Kind == ProcedureHandlerKind.Separator);
			Assert.Contains(catalog.Entries, e => e.Kind == ProcedureHandlerKind.Group && e.Children.Count >= 3);
		}

		[Fact]
		public async Task LoadAsync_MissingFile_ReturnsEmptyCatalog()
		{
			ProcedureHandlerCatalog catalog = await ProcedureHandlerCatalog.LoadAsync(System.IO.Path.Combine(TestFiles.Work, "nope-" + Guid.NewGuid().ToString("N") + ".ini"));

			Assert.Empty(catalog.Entries);
		}
	}
}