namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Tests.Functions;
	using Xunit;

	public class FlowchartDocumentTests : IDisposable
	{
		/// <summary>A file in the layout the legacy editor wrote.</summary>
		private const String LegacyXml =
			"<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\r\n"
			+ "<NodesDiagram ScriptName=\"dcjohn.ssl\" Zoom=\"0.75\" PositionX=\"120\" PositionY=\"40\">\r\n"
			+ "  <Node Name=\"Node999\" Collapsed=\"True\" Hidden=\"True\">\r\n"
			+ "    <Position X=\"0.5\" Y=\"1.25\" Width=\"2.5\" />\r\n"
			+ "  </Node>\r\n"
			+ "  <Node Name=\"talk_p_proc\" Collapsed=\"False\" Hidden=\"False\" ShowCode=\"True\">\r\n"
			+ "    <Position X=\"0.25\" Y=\"0.25\" Width=\"3\" />\r\n"
			+ "    <Contents>\r\n"
			+ "      <Content Line=\"0\" Collapsed=\"False\" />\r\n"
			+ "      <Content Line=\"1\" Collapsed=\"True\" />\r\n"
			+ "    </Contents>\r\n"
			+ "  </Node>\r\n"
			+ "  <Note NoteText=\"Remember the quest flag\">\r\n"
			+ "    <Position X=\"0.4\" Y=\"0.4\" Width=\"2\" Height=\"0.5\" />\r\n"
			+ "  </Note>\r\n"
			+ "</NodesDiagram>";

		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		[Fact]
		public void Parse_ReadsTheLegacyFile()
		{
			FlowchartDocument document = FlowchartDocumentSerializer.Parse(LegacyXml);

			Assert.Equal("dcjohn.ssl", document.ScriptName);
			Assert.Equal((0.75f, 120, 40), (document.Zoom, document.PositionX, document.PositionY));
			Assert.Equal(new[] { "Node999", "talk_p_proc" }, document.Nodes.Select(n => n.Name).ToArray());

			FlowchartNodeState hidden = document.Nodes[0];
			Assert.True(hidden.Collapsed);
			Assert.True(hidden.Hidden);
			Assert.Null(hidden.ShowCode);
			Assert.Equal((50f, 125f, 250f), (hidden.X, hidden.Y, hidden.Width));
			Assert.Empty(hidden.Contents);

			FlowchartNodeState talk = document.Nodes[1];
			Assert.False(talk.Collapsed);
			Assert.True(talk.ShowCode);
			Assert.Equal(new[] { new FlowchartContentState("0", false), new FlowchartContentState("1", true) }, talk.Contents.ToArray());

			FlowchartNoteState note = Assert.Single(document.Notes);
			Assert.Equal("Remember the quest flag", note.Text);
			Assert.Equal((40f, 40f, 200f, 50f), (note.X, note.Y, note.Width, note.Height));
		}

		[Fact]
		public void Serialize_ProducesTheLegacyLayout()
		{
			FlowchartDocument document = FlowchartDocumentSerializer.Parse(LegacyXml);

			String xml = FlowchartDocumentSerializer.Serialize(document);

			Assert.Equal(LegacyXml, xml);
		}

		[Fact]
		public void ToFileBytes_StartsWithAByteOrderMark_AndCanBeReadBack()
		{
			FlowchartDocument document = FlowchartDocumentSerializer.Parse(LegacyXml);

			Byte[] bytes = FlowchartDocumentSerializer.ToFileBytes(document);

			Assert.Equal(new Byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
			Assert.Equal(document.Nodes.Count, FlowchartDocumentSerializer.Parse(new System.Text.UTF8Encoding(true).GetString(bytes)).Nodes.Count);
		}

		[Fact]
		public void Serialize_EscapesTextAndKeepsInvariantNumbers()
		{
			var document = new FlowchartDocument("a&b.ssl")
			{
				Zoom = 1.5f,
				Nodes = new[] { new FlowchartNodeState("N<1>") { X = 25, Y = 12.5f, Width = 100, ShowCode = false } },
				Notes = new[] { new FlowchartNoteState("say \"hi\" & <bye>\r\nnext") },
			};
			System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
			System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
			try
			{
				String xml = FlowchartDocumentSerializer.Serialize(document);

				Assert.Contains("Zoom=\"1.5\"", xml);
				Assert.Contains("Y=\"0.125\"", xml);
				FlowchartDocument back = FlowchartDocumentSerializer.Parse(xml);
				Assert.Equal("a&b.ssl", back.ScriptName);
				Assert.Equal("N<1>", back.Nodes[0].Name);
				Assert.False(back.Nodes[0].ShowCode);
				Assert.Equal(12.5f, back.Nodes[0].Y);
				Assert.Contains("say \"hi\" & <bye>", back.Notes[0].Text);
			}
			finally
			{
				System.Globalization.CultureInfo.CurrentCulture = previous;
			}
		}

		[Fact]
		public void Parse_MissingOrBadAttributes_GetDefaults()
		{
			FlowchartDocument document = FlowchartDocumentSerializer.Parse("<NodesDiagram><Node Name=\"x\" Collapsed=\"maybe\"/><Node/><Note/></NodesDiagram>");

			Assert.Equal(1f, document.Zoom);
			Assert.Equal(0, document.PositionX);
			Assert.Equal(2, document.Nodes.Count);
			Assert.False(document.Nodes[0].Collapsed);
			Assert.Equal(String.Empty, document.Nodes[1].Name);
			Assert.Equal(String.Empty, Assert.Single(document.Notes).Text);
		}

		[Theory]
		[InlineData("")]
		[InlineData("not xml")]
		[InlineData("<Other/>")]
		public void Parse_NotAFlowchart_ThrowsFormatException(String xml)
		{
			Assert.Throws<FormatException>(() => FlowchartDocumentSerializer.Parse(xml));
			Assert.False(FlowchartDocumentSerializer.TryParse(xml, out FlowchartDocument? document, out String? error));
			Assert.Null(document);
			Assert.NotNull(error);
		}

		[Fact]
		public void IsForScript_ComparesFileNamesIgnoringCase()
		{
			FlowchartDocument document = FlowchartDocumentSerializer.Parse(LegacyXml);

			Assert.True(document.IsForScript(@"C:\mod\scripts\DCJohn.SSL"));
			Assert.False(document.IsForScript(@"C:\mod\scripts\other.ssl"));
			Assert.Equal("dcjohn.ssl", FlowchartDocument.CreateScriptName(@"C:\mod\scripts\DCJohn.SSL"));
		}

		[Fact]
		public void Reconcile_ReportsAddedAndRemovedNodes()
		{
			FlowchartDocument document = FlowchartDocumentSerializer.Parse(LegacyXml);

			FlowchartReconciliation result = document.Reconcile(new[] { "TALK_P_PROC", "Node001", "node001" });

			Assert.Equal(new[] { "Node001" }, result.AddedNodes.ToArray());
			Assert.Equal(new[] { "Node999" }, result.RemovedNodes.ToArray());
			Assert.Equal(new[] { "talk_p_proc" }, result.Document.Nodes.Select(n => n.Name).ToArray());
			Assert.Equal(2, document.Nodes.Count);
		}

		[Fact]
		public void FindNode_IsCaseInsensitive()
		{
			FlowchartDocument document = FlowchartDocumentSerializer.Parse(LegacyXml);

			Assert.NotNull(document.FindNode("NODE999"));
			Assert.Null(document.FindNode("missing"));
		}

		[Fact]
		public void SearchPaths_AreNextToTheScriptAndInTheFcdFolder()
		{
			Assert.Equal(
				new[] { @"C:\mod\scripts\dcjohn.fcd", @"C:\mod\scripts\fcd\dcjohn.fcd" },
				FlowchartDocumentStore.GetSearchPaths(@"C:\mod\scripts\dcjohn.ssl").ToArray());
			Assert.Equal(@"C:\mod\scripts\dcjohn.fcd", FlowchartDocumentStore.GetDefaultSavePath(@"C:\mod\scripts\dcjohn.ssl"));
		}

		[Fact]
		public async Task Store_FindsLoadsAndSaves()
		{
			var store = new FlowchartDocumentStore();
			String script = this._folder.Write("scripts\\dcjohn.ssl", "// script");

			Assert.Null(store.FindFile(script));

			await store.SaveAsync(FlowchartDocumentStore.GetDefaultSavePath(script), FlowchartDocumentSerializer.Parse(LegacyXml));
			Assert.Equal(this._folder.Combine("scripts\\dcjohn.fcd"), store.FindFile(script));
			FlowchartDocument loaded = await store.LoadAsync(store.FindFile(script)!);

			Assert.Equal(2, loaded.Nodes.Count);
			Assert.Empty(Directory.GetFiles(this._folder.Combine("scripts"), "*.tmp"));
		}

		[Fact]
		public async Task Store_SaveWritesSynchronouslyAndAtomically()
		{
			var store = new FlowchartDocumentStore();
			String script = this._folder.Write("scripts\\dcjohn.ssl", "// script");

			store.Save(FlowchartDocumentStore.GetDefaultSavePath(script), FlowchartDocumentSerializer.Parse(LegacyXml));
			FlowchartDocument loaded = await store.LoadAsync(this._folder.Combine("scripts\\dcjohn.fcd"));

			Assert.Equal(2, loaded.Nodes.Count);
			Assert.Empty(Directory.GetFiles(this._folder.Combine("scripts"), "*.tmp"));
		}

		[Fact]
		public async Task Store_FindsTheFileInTheFcdSubFolder()
		{
			var store = new FlowchartDocumentStore();
			String script = this._folder.Write("scripts\\dcjohn.ssl", "// script");
			String file = this._folder.Write("scripts\\fcd\\dcjohn.fcd", LegacyXml);

			Assert.Equal(file, store.FindFile(script));
			Assert.Equal("dcjohn.ssl", (await store.LoadAsync(file)).ScriptName);
		}

		[Fact]
		public async Task Store_LoadingAnInvalidFile_Throws()
		{
			var store = new FlowchartDocumentStore();
			String file = this._folder.Write("bad.fcd", "<oops");

			await Assert.ThrowsAsync<FormatException>(() => store.LoadAsync(file));
		}
	}
}
