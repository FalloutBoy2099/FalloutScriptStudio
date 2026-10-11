namespace FalloutScriptStudio.Core.Tests.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.Tests.Functions;
	using Xunit;

	public class RefactorDocumentIOTests : IDisposable
	{
		private readonly TempFolder _folder = new TempFolder();
		private readonly RefactoringService _service = new RefactoringService();

		public void Dispose()
		{
			this._folder.Dispose();
		}

		[Fact]
		public void ReadFiles_ReadsLazily_SkippingGivenAndUnreadablePaths()
		{
			String a = this._folder.Write("a.ssl", "alpha");
			String b = this._folder.Write("b.ssl", "beta");
			String missing = this._folder.Combine("missing.ssl");

			var documents = RefactorDocumentIO.ReadFiles(new[] { a, missing, b }, new HashSet<String> { b }, Encoding.UTF8).ToList();

			Assert.Equal(new RefactorDocument(a, "alpha"), Assert.Single(documents));
		}

		[Fact]
		public async Task GlobalMacroRename_EndToEnd_RewritesClosedFilesOnly()
		{
			String header = this._folder.Write("headers\\macros.h", "#define OLD_NAME   5\r\n");
			String user = this._folder.Write("scripts\\a.ssl", "x := OLD_NAME + OLD_NAME_2;\r\n");
			String untouched = this._folder.Write("scripts\\b.ssl", "y := 1;\r\n");
			var macro = new ScriptMacro("OLD_NAME", "OLD_NAME", "5", header, 1, null);
			String openText = "z := OLD_NAME;";
			var open = new RefactorDocument(@"C:\open\c.ssl", openText, true);

			IEnumerable<RefactorDocument> project = RefactorDocumentIO.ReadFiles(ProjectFileFinderForTest(), null, Encoding.UTF8);
			MacroRenamePreview preview = this._service.PreviewMacroRename(macro, "NEW", project.Append(open));
			RenameResult result = this._service.ApplyMacroRename(preview, preview.Matches);
			IReadOnlyList<String> written = await RefactorDocumentIO.WriteClosedFilesAsync(result, preview.Documents, Encoding.UTF8);

			Assert.Equal(new[] { header, user }.OrderBy(p => p).ToArray(), written.OrderBy(p => p).ToArray());
			Assert.Equal("#define NEW        5\r\n", File.ReadAllText(header));
			Assert.Equal("x := NEW + OLD_NAME_2;\r\n", File.ReadAllText(user));
			Assert.Equal("y := 1;\r\n", File.ReadAllText(untouched));
			Assert.Equal("z := NEW;", result.Files.Single(f => f.IsOpen).ApplyTo(openText));
			Assert.Empty(Directory.GetFiles(this._folder.Combine("scripts"), "*.tmp"));
		}

		private IEnumerable<String> ProjectFileFinderForTest()
		{
			return Directory.GetFiles(this._folder.Path, "*.*", SearchOption.AllDirectories);
		}
	}
}
