namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Text;
	using FalloutScriptStudio.Workspace;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class DocumentTextCodecTests
	{
		private static Byte[] Join(Byte[] first, Byte[] second)
		{
			Byte[] result = new Byte[first.Length + second.Length];
			first.CopyTo(result, 0);
			second.CopyTo(result, first.Length);
			return result;
		}

		[Fact]
		public void A_byte_order_mark_wins()
		{
			Byte[] bytes = Join(Encoding.UTF8.GetPreamble(), Encoding.UTF8.GetBytes("héllo"));

			DecodedText decoded = DocumentTextCodec.Decode(bytes, Encoding.Latin1);

			Assert.Equal("héllo", decoded.Text);
			Assert.Equal(65001, decoded.Encoding.CodePage);
		}

		[Fact]
		public void Utf16_with_a_byte_order_mark_is_detected()
		{
			Byte[] bytes = Join(Encoding.Unicode.GetPreamble(), Encoding.Unicode.GetBytes("abc"));

			Assert.Equal("abc", DocumentTextCodec.Decode(bytes, Encoding.Latin1).Text);
		}

		[Fact]
		public void Valid_utf8_without_a_mark_is_detected_when_it_has_non_ascii_characters()
		{
			DecodedText decoded = DocumentTextCodec.Decode(new UTF8Encoding(false).GetBytes("café"), Encoding.Latin1);

			Assert.Equal("café", decoded.Text);
			Assert.Equal(65001, decoded.Encoding.CodePage);
		}

		[Fact]
		public void Other_bytes_use_the_fallback_code_page()
		{
			Encoding cyrillic = FalloutScriptStudio.Core.IO.TextEncodings.Oem866;
			Byte[] bytes = { 0x8F, 0xF0, 0xA8, 0xA2, 0xA5, 0xE2 };

			DecodedText decoded = DocumentTextCodec.Decode(bytes, cyrillic);

			Assert.Equal(cyrillic.GetString(bytes), decoded.Text);
			Assert.Same(cyrillic, decoded.Encoding);
		}

		[Fact]
		public void Plain_ascii_uses_the_fallback_encoding()
		{
			DecodedText decoded = DocumentTextCodec.Decode(Encoding.ASCII.GetBytes("abc"), Encoding.Latin1);

			Assert.Same(Encoding.Latin1, decoded.Encoding);
		}

		[Theory]
		[InlineData("a\nb", "a\r\nb")]
		[InlineData("a\rb", "a\r\nb")]
		[InlineData("a\r\nb\n\rc", "a\r\nb\r\n\r\nc")]
		[InlineData("a\r\nb", "a\r\nb")]
		[InlineData("", "")]
		[InlineData("trailing\r", "trailing\r\n")]
		public void Line_endings_are_normalized(String input, String expected)
		{
			Assert.Equal(expected, DocumentTextCodec.NormalizeNewLines(input));
		}

		[Fact]
		public void Trailing_whitespace_is_trimmed_as_one_undo_step()
		{
			TextDocument document = new TextDocument("a  \r\n\t\r\nb\t \r\nc");

			Boolean changed = DocumentTextCodec.TrimTrailingWhitespace(document);

			Assert.True(changed);
			Assert.Equal("a\r\n\r\nb\r\nc", document.Text);
			document.UndoStack.Undo();
			Assert.Equal("a  \r\n\t\r\nb\t \r\nc", document.Text);
		}

		[Fact]
		public void A_clean_document_is_left_unchanged()
		{
			TextDocument document = new TextDocument("a\r\nb");

			Assert.False(DocumentTextCodec.TrimTrailingWhitespace(document));
			Assert.False(document.UndoStack.CanUndo);
		}
	}
}
