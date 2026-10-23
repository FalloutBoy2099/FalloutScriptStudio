namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public class MessageComparisonServiceTests
	{
		private const String ReferenceText = "# NPC\r\n{100}{}{Hello.}\r\n\t{101}{}{Bye.}# polite\r\n{102}{}{Two\r\nlines.}\r\n{103}{}{Café %s}\r\n";

		private static readonly MessageComparisonService _service = new();

		private static Encoding Western => TextEncodings.FromCodePage(1252);

		private static String WriteBytes(TestFolder folder, String relative, Byte[] bytes)
		{
			String path = folder.Combine(relative);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, bytes);
			return path;
		}

		private static IEnumerable<MessageEntry> Missing(MessageComparison comparison)
		{
			return comparison.Rows.Where(row => row.Status == MessageComparisonStatus.MissingInTarget).Select(row => row.Reference!);
		}

		private static async Task<(LoadedMessageFile Reference, String TargetPath)> PrepareAsync(TestFolder folder, Byte[] target, String referenceText = ReferenceText)
		{
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes(referenceText));
			String french = WriteBytes(folder, @"text\french\dialog\a.msg", target);
			return (await _service.LoadAsync(english, Western, true), french);
		}

		[Fact]
		public async Task The_reference_is_read_with_the_encoding_of_the_settings_and_a_target_is_detected()
		{
			using TestFolder folder = new("compare-load");
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes("{1}{}{Voilà.}\r\n"));
			String french = WriteBytes(folder, @"text\french\dialog\a.msg", new UTF8Encoding(false).GetBytes("{1}{}{Voilà.}\r\n"));

			LoadedMessageFile reference = await _service.LoadAsync(english, Western, true);
			LoadedMessageFile target = await _service.LoadAsync(french, Western, false);
			LoadedMessageFile forced = await _service.LoadAsync(french, Western, true);

			Assert.Equal("english", reference.Language);
			Assert.Equal("french", target.Language);
			Assert.Equal(1252, reference.Decoded.Encoding.CodePage);
			Assert.Equal(65001, target.Decoded.Encoding.CodePage);
			Assert.Equal("Voilà.", target.Document.FindMessage(1)!.Text);
			Assert.Equal(1252, forced.Decoded.Encoding.CodePage);
			Assert.NotEqual("Voilà.", forced.Document.FindMessage(1)!.Text);
		}

		[Fact]
		public async Task A_target_that_is_not_utf8_is_read_with_the_code_page_of_its_language()
		{
			using TestFolder folder = new("compare-codepage");
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes("{1}{}{Hello.}\r\n"));
			String russian = WriteBytes(folder, @"text\russian\dialog\a.msg", TextEncodings.FromCodePage(1251).GetBytes("{1}{}{Привет.}\r\n"));
			String polish = WriteBytes(folder, @"text\polish\dialog\a.msg", TextEncodings.FromCodePage(1250).GetBytes("{1}{}{Cześć.}\r\n"));
			String dos = WriteBytes(folder, @"text\russian\dialog\dos.msg", TextEncodings.Oem866.GetBytes("{1}{}{Привет.}\r\n"));

			LoadedMessageFile reference = await _service.LoadAsync(english, Western, true);
			LoadedMessageFile cyrillic = await _service.LoadAsync(russian, Western, false);
			LoadedMessageFile central = await _service.LoadAsync(polish, Western, false);
			LoadedMessageFile legacy = await _service.LoadAsync(dos, TextEncodings.Oem866, false);

			Assert.Equal("Привет.", cyrillic.Document.FindMessage(1)!.Text);
			Assert.Equal("Cześć.", central.Document.FindMessage(1)!.Text);
			Assert.Equal("Привет.", legacy.Document.FindMessage(1)!.Text);
			Assert.DoesNotContain(_service.Compare(reference, cyrillic).Notes, note => note.Kind == MessageComparisonNoteKind.SuspiciousEncoding);
		}

		[Fact]
		public async Task Two_files_in_different_encodings_get_a_note()
		{
			using TestFolder folder = new("compare-notes");
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes("{1}{}{Hello.}\r\n"));
			String french = WriteBytes(folder, @"text\french\dialog\a.msg", new Byte[] { 0xEF, 0xBB, 0xBF }.Concat(new UTF8Encoding(false).GetBytes("{1}{}{Bonjour.}\r\n")).ToArray());
			String german = WriteBytes(folder, @"text\german\dialog\a.msg", Western.GetBytes("{1}{}{Hallo.}\r\n"));

			LoadedMessageFile reference = await _service.LoadAsync(english, Western, true);

			MessageComparison utf8 = _service.Compare(reference, await _service.LoadAsync(french, Western, false));
			MessageComparison same = _service.Compare(reference, await _service.LoadAsync(german, Western, false));

			MessageComparisonNote note = Assert.Single(utf8.Notes, n => n.Kind == MessageComparisonNoteKind.EncodingDiffers);
			Assert.Contains("UTF-8 (BOM)", note.Detail, StringComparison.Ordinal);
			Assert.Empty(same.Notes);
		}

		[Fact]
		public async Task A_target_read_with_the_wrong_encoding_gets_a_note()
		{
			using TestFolder folder = new("compare-wrong");
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes("{1}{}{Hello.}\r\n"));
			String french = WriteBytes(folder, @"text\french\dialog\a.msg", Western.GetBytes("{1}{}{Bon").Concat(new Byte[] { 0x81 }).Concat(Western.GetBytes("jour.}\r\n")).ToArray());

			MessageComparison comparison = _service.Compare(await _service.LoadAsync(english, Western, true), await _service.LoadAsync(french, Western, false));

			Assert.Contains(comparison.Notes, note => note.Kind == MessageComparisonNoteKind.SuspiciousEncoding);
			Assert.True((comparison.Rows.Single().Issues & MessageComparisonIssues.SuspiciousCharacters) != 0);
		}

		[Fact]
		public async Task The_files_of_the_other_languages_are_offered()
		{
			using TestFolder folder = new("compare-siblings");
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes("{1}{}{Hello.}\r\n"));
			WriteBytes(folder, @"text\french\dialog\a.msg", Western.GetBytes("{1}{}{Bonjour.}\r\n"));
			await Task.CompletedTask;

			MessageLanguageFile file = Assert.Single(_service.FindSiblings(english));

			Assert.Equal("french", file.Language);
		}

		[Fact]
		public async Task Adding_the_missing_lines_appends_them_after_the_last_line_and_touches_nothing_else()
		{
			using TestFolder folder = new("compare-add");
			Byte[] original = Western.GetBytes("{100}{}{Bonjour.}\r\n{103}{}{Café %s fr}\r\n\r\n");
			(LoadedMessageFile reference, String path) = await PrepareAsync(folder, original);
			LoadedMessageFile target = await _service.LoadAsync(path, Western, false);
			MessageComparison comparison = _service.Compare(reference, target);

			MessageAppendResult result = await _service.AddMissingAsync(target, Missing(comparison), null);

			Assert.Equal(new[] { 101, 102 }, result.Added);
			Assert.Empty(result.NotEncodable);
			String expected = "{100}{}{Bonjour.}\r\n{103}{}{Café %s fr}\r\n\t{101}{}{Bye.}# polite\r\n{102}{}{Two\r\nlines.}\r\n\r\n";
			Assert.Equal(Western.GetBytes(expected), File.ReadAllBytes(path));
			Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
		}

		[Fact]
		public async Task Adding_the_missing_lines_twice_changes_nothing_the_second_time()
		{
			using TestFolder folder = new("compare-add-twice");
			(LoadedMessageFile reference, String path) = await PrepareAsync(folder, Western.GetBytes("{100}{}{Bonjour.}\r\n"));
			LoadedMessageFile target = await _service.LoadAsync(path, Western, false);
			await _service.AddMissingAsync(target, Missing(_service.Compare(reference, target)), null);
			Byte[] after = File.ReadAllBytes(path);

			LoadedMessageFile again = await _service.LoadAsync(path, Western, false);
			MessageComparison second = _service.Compare(reference, again);
			MessageAppendResult result = await _service.AddMissingAsync(again, Missing(second), null);
			MessageAppendResult stale = await _service.AddMissingAsync(target, Missing(_service.Compare(reference, target)), null);

			Assert.Empty(second.GetMissingNumbers());
			Assert.Empty(result.Added);
			Assert.Empty(stale.Added);
			Assert.Equal(after, File.ReadAllBytes(path));
		}

		[Theory]
		[InlineData("\r\n", true)]
		[InlineData("\n", true)]
		[InlineData("\r\n", false)]
		[InlineData("\n", false)]
		public async Task The_line_breaks_and_the_final_break_of_the_file_are_kept(String newLine, Boolean finalBreak)
		{
			using TestFolder folder = new("compare-eol");
			String content = "{100}{}{Bonjour.}" + newLine + "{103}{}{Salut.}" + (finalBreak ? newLine : String.Empty);
			(LoadedMessageFile reference, String path) = await PrepareAsync(folder, Western.GetBytes(content));
			LoadedMessageFile target = await _service.LoadAsync(path, Western, false);

			await _service.AddMissingAsync(target, Missing(_service.Compare(reference, target)), null);

			String expected = "{100}{}{Bonjour.}" + newLine + "{103}{}{Salut.}" + newLine + "\t{101}{}{Bye.}# polite" + newLine + "{102}{}{Two" + newLine + "lines.}" + (finalBreak ? newLine : String.Empty);
			Assert.Equal(expected, Western.GetString(File.ReadAllBytes(path)));
		}

		[Fact]
		public async Task A_utf8_file_keeps_its_byte_order_mark_and_one_without_stays_without()
		{
			using TestFolder folder = new("compare-bom");
			String reference = "{100}{}{Hello.}\r\n{101}{}{Voilà.}\r\n";
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes(reference));
			String withBom = WriteBytes(folder, @"text\french\dialog\a.msg", new Byte[] { 0xEF, 0xBB, 0xBF }.Concat(new UTF8Encoding(false).GetBytes("{100}{}{Bonjour é.}\r\n")).ToArray());
			String withoutBom = WriteBytes(folder, @"text\german\dialog\a.msg", new UTF8Encoding(false).GetBytes("{100}{}{Hallo ü.}\r\n"));
			LoadedMessageFile source = await _service.LoadAsync(english, Western, true);

			foreach (String path in new[] { withBom, withoutBom })
			{
				LoadedMessageFile target = await _service.LoadAsync(path, Western, false);
				await _service.AddMissingAsync(target, Missing(_service.Compare(source, target)), null);
			}

			Byte[] bom = File.ReadAllBytes(withBom);
			Byte[] plain = File.ReadAllBytes(withoutBom);
			Assert.Equal(new Byte[] { 0xEF, 0xBB, 0xBF }, bom.Take(3).ToArray());
			Assert.NotEqual(new Byte[] { 0xEF, 0xBB, 0xBF }, plain.Take(3).ToArray());
			Assert.Equal("{100}{}{Bonjour é.}\r\n{101}{}{Voilà.}\r\n", new UTF8Encoding(false).GetString(bom, 3, bom.Length - 3));
			Assert.Equal("{100}{}{Hallo ü.}\r\n{101}{}{Voilà.}\r\n", new UTF8Encoding(false).GetString(plain));
		}

		[Fact]
		public async Task A_utf16_file_is_written_back_as_utf16_with_its_byte_order_mark()
		{
			using TestFolder folder = new("compare-utf16");
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", Western.GetBytes("{100}{}{Hello.}\r\n{101}{}{Bye.}\r\n"));
			Byte[] utf16 = new Byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("{100}{}{Bonjour.}\r\n")).ToArray();
			String french = WriteBytes(folder, @"text\french\dialog\a.msg", utf16);
			LoadedMessageFile reference = await _service.LoadAsync(english, Western, true);
			LoadedMessageFile target = await _service.LoadAsync(french, Western, false);

			await _service.AddMissingAsync(target, Missing(_service.Compare(reference, target)), null);

			Byte[] after = File.ReadAllBytes(french);
			Assert.Equal(new Byte[] { 0xFF, 0xFE }, after.Take(2).ToArray());
			Assert.Equal("{100}{}{Bonjour.}\r\n{101}{}{Bye.}\r\n", Encoding.Unicode.GetString(after, 2, after.Length - 2));
		}

		[Fact]
		public async Task The_marker_is_written_after_the_closing_brace_next_to_the_description_of_the_reference()
		{
			using TestFolder folder = new("compare-marker");
			(LoadedMessageFile reference, String path) = await PrepareAsync(folder, Western.GetBytes("{100}{}{Bonjour.}\r\n"));
			LoadedMessageFile target = await _service.LoadAsync(path, Western, false);

			await _service.AddMissingAsync(target, Missing(_service.Compare(reference, target)), "# TODO: translate");

			String text = Western.GetString(File.ReadAllBytes(path));
			Assert.Contains("\t{101}{}{Bye.}# polite # TODO: translate\r\n", text, StringComparison.Ordinal);
			Assert.Contains("{102}{}{Two\r\nlines.}# TODO: translate\r\n", text, StringComparison.Ordinal);
			Assert.Contains("{103}{}{Café %s}# TODO: translate\r\n", text, StringComparison.Ordinal);
			LoadedMessageFile reread = await _service.LoadAsync(path, Western, false);
			Assert.Equal("Café %s", reread.Document.FindMessage(103)!.Text);
			Assert.True(reread.Document.FindMessage(101)!.IsPlayerLine);
		}

		[Fact]
		public async Task A_text_that_the_encoding_of_the_target_cannot_hold_is_left_out_and_reported()
		{
			using TestFolder folder = new("compare-notencodable");
			String english = WriteBytes(folder, @"text\english\dialog\a.msg", new UTF8Encoding(false).GetBytes("{100}{}{Hello.}\r\n{101}{}{Привет}\r\n{102}{}{Bye.}\r\n"));
			String french = WriteBytes(folder, @"text\french\dialog\a.msg", Western.GetBytes("{100}{}{Bonjour.}\r\n"));
			LoadedMessageFile reference = await _service.LoadAsync(english, Western, false);
			LoadedMessageFile target = await _service.LoadAsync(french, Western, false);

			MessageAppendResult result = await _service.AddMissingAsync(target, Missing(_service.Compare(reference, target)), null);

			Assert.Equal(new[] { 102 }, result.Added);
			Assert.Equal(new[] { 101 }, result.NotEncodable);
			Assert.Equal("{100}{}{Bonjour.}\r\n{102}{}{Bye.}\r\n", Western.GetString(File.ReadAllBytes(french)));
		}

		[Fact]
		public async Task Nothing_is_written_when_there_is_nothing_to_add()
		{
			using TestFolder folder = new("compare-nothing");
			(LoadedMessageFile reference, String path) = await PrepareAsync(folder, Western.GetBytes("{100}{}{A.}\r\n"), "{100}{}{B.}\r\n");
			LoadedMessageFile target = await _service.LoadAsync(path, Western, false);
			DateTime before = File.GetLastWriteTimeUtc(path);

			MessageAppendResult result = await _service.AddMissingAsync(target, Missing(_service.Compare(reference, target)), null);

			Assert.Empty(result.Added);
			Assert.Equal(before, File.GetLastWriteTimeUtc(path));
		}

		[Fact]
		public async Task A_report_is_written_as_utf8_with_a_byte_order_mark()
		{
			using TestFolder folder = new("compare-report");
			String path = folder.Combine("report.csv");

			await _service.WriteReportAsync(path, "a,é\r\n");

			Byte[] bytes = File.ReadAllBytes(path);
			Assert.Equal(new Byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
			Assert.Equal("a,é\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
		}
	}
}