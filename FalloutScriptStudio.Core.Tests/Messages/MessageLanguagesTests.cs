namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public class MessageLanguagesTests
	{
		[Theory]
		[InlineData(@"C:\Fallout2\data\text\english\dialog\hakunin.msg", "english")]
		[InlineData(@"C:\Fallout2\data\TEXT\French\dialog\hakunin.msg", "French")]
		[InlineData(@"C:\mod\text\klingon\dialog\x.msg", "klingon")]
		[InlineData(@"C:\mod\text\russian\game\combat.msg", "russian")]
		[InlineData(@"C:\mod\german\dialog\x.msg", "german")]
		[InlineData(@"C:\mod\dialog\x.msg", null)]
		[InlineData(@"C:\mod\text\x.msg", null)]
		public void The_language_of_a_file_is_the_folder_below_text(String path, String? expected)
		{
			Assert.Equal(expected, MessageLanguages.DetectLanguage(path));
		}

		[Fact]
		public void The_known_folders_are_the_languages_of_the_game()
		{
			Assert.Contains("english", MessageLanguages.KnownFolders);
			Assert.Contains("french", MessageLanguages.KnownFolders);
			Assert.Contains("german", MessageLanguages.KnownFolders);
			Assert.Contains("spanish", MessageLanguages.KnownFolders);
			Assert.Contains("italian", MessageLanguages.KnownFolders);
			Assert.Contains("russian", MessageLanguages.KnownFolders);
			Assert.Contains("polish", MessageLanguages.KnownFolders);
			Assert.True(MessageLanguages.IsKnown("ENGLISH"));
			Assert.False(MessageLanguages.IsKnown("dialog"));
		}

		[Theory]
		[InlineData("english", 1252)]
		[InlineData("french", 1252)]
		[InlineData("german", 1252)]
		[InlineData("spanish", 1252)]
		[InlineData("italian", 1252)]
		[InlineData("russian", 1251)]
		[InlineData("polish", 1250)]
		[InlineData("czech", 1250)]
		[InlineData("greek", 1253)]
		public void Every_language_has_its_code_page_whatever_the_encoding_of_the_settings(String language, Int32 codePage)
		{
			Assert.Equal(codePage, MessageLanguages.GetEncoding(language, TextEncodings.Ansi).CodePage);
			Assert.Equal(codePage, MessageLanguages.GetEncoding(language.ToUpperInvariant(), TextEncodings.Utf8).CodePage);
		}

		[Fact]
		public void The_dos_cyrillic_option_of_the_settings_wins_for_russian_only()
		{
			Assert.Equal(866, MessageLanguages.GetEncoding("russian", TextEncodings.Oem866).CodePage);
			Assert.Equal(1252, MessageLanguages.GetEncoding("french", TextEncodings.Oem866).CodePage);
		}

		[Fact]
		public void A_language_that_the_table_does_not_know_uses_the_encoding_of_the_settings()
		{
			Assert.Same(TextEncodings.Oem866, MessageLanguages.GetEncoding("klingon", TextEncodings.Oem866));
			Assert.Same(TextEncodings.Oem866, MessageLanguages.GetEncoding(null, TextEncodings.Oem866));
		}

		[Fact]
		public void The_files_of_the_other_languages_are_found_beside_the_language_folder()
		{
			using TestFolder folder = new("languages");
			String english = folder.Write(@"data\text\english\dialog\hakunin.msg", "{1}{}{Hello.}");
			String french = folder.Write(@"data\text\french\dialog\hakunin.msg", "{1}{}{Bonjour.}");
			folder.Write(@"data\text\german\dialog\other.msg", "{1}{}{Hallo.}");
			String klingon = folder.Write(@"data\text\klingon\dialog\hakunin.msg", "{1}{}{nuqneH.}");
			Directory.CreateDirectory(folder.Combine(@"data\text\spanish\dialog"));

			MessageLanguageFile[] found = MessageLanguages.FindSiblings(english).ToArray();

			Assert.Equal(new[] { "french", "klingon" }, found.Select(file => file.Language).ToArray());
			Assert.Equal(new[] { french, klingon }, found.Select(file => file.Path).ToArray());
			Assert.Equal(new[] { "english", "klingon" }, MessageLanguages.FindSiblings(french).Select(file => file.Language).ToArray());
		}

		[Fact]
		public void A_file_outside_a_text_folder_has_no_sibling()
		{
			using TestFolder folder = new("languages-none");
			String path = folder.Write(@"mod\dialog\x.msg", "{1}{}{Hello.}");

			Assert.Empty(MessageLanguages.FindSiblings(path));
		}

		[Fact]
		public void The_code_pages_give_the_characters_of_their_language()
		{
			Assert.Equal("é", MessageLanguages.GetEncoding("french", TextEncodings.Ansi).GetString(new Byte[] { 0xE9 }));
			Assert.Equal("Я", MessageLanguages.GetEncoding("russian", TextEncodings.Ansi).GetString(new Byte[] { 0xDF }));
			Assert.Equal("Я", MessageLanguages.GetEncoding("russian", TextEncodings.Oem866).GetString(new Byte[] { 0x9F }));
			Assert.Equal("ł", MessageLanguages.GetEncoding("polish", TextEncodings.Ansi).GetString(new Byte[] { 0xB3 }));
			Assert.Equal(Encoding.GetEncoding(1250).CodePage, MessageLanguages.GetEncoding("polish", TextEncodings.Ansi).CodePage);
		}
	}
}