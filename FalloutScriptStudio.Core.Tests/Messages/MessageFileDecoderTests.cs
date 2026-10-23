namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using Xunit;

	public class MessageFileDecoderTests
	{
		private static Byte[] Bytes(Encoding encoding, String text, Boolean bom = false)
		{
			Byte[] body = encoding.GetBytes(text);
			if (!bom)
			{
				return body;
			}

			Byte[] preamble = encoding.CodePage == 65001 ? new Byte[] { 0xEF, 0xBB, 0xBF } : encoding.CodePage == 1200 ? new Byte[] { 0xFF, 0xFE } : new Byte[] { 0xFE, 0xFF };
			return preamble.Concat(body).ToArray();
		}

		[Fact]
		public void A_byte_order_mark_wins_over_the_encoding_given()
		{
			DecodedMessageFile utf8 = MessageFileDecoder.Decode(Bytes(Encoding.UTF8, "{1}{}{é}\r\n", true), TextEncodings.Oem866, false);
			DecodedMessageFile le = MessageFileDecoder.Decode(Bytes(Encoding.Unicode, "{1}{}{é}\r\n", true), TextEncodings.Ansi, false);
			DecodedMessageFile be = MessageFileDecoder.Decode(Bytes(Encoding.BigEndianUnicode, "{1}{}{é}\r\n", true), TextEncodings.Ansi, false);

			Assert.All(new[] { utf8, le, be }, file =>
			{
				Assert.True(file.HasByteOrderMark);
				Assert.Equal("{1}{}{é}", Assert.Single(file.Lines));
				Assert.False(file.UsedFallback);
			});
			Assert.Equal(65001, utf8.Encoding.CodePage);
			Assert.Equal(1200, le.Encoding.CodePage);
			Assert.Equal(1201, be.Encoding.CodePage);
		}

		[Fact]
		public void Valid_utf8_with_non_ascii_characters_is_recognised_only_when_detecting()
		{
			Byte[] bytes = Bytes(new UTF8Encoding(false), "{1}{}{Voilà, être.}");

			DecodedMessageFile detected = MessageFileDecoder.Decode(bytes, TextEncodings.Ansi, true);
			DecodedMessageFile forced = MessageFileDecoder.Decode(bytes, TextEncodings.Ansi, false);

			Assert.Equal(65001, detected.Encoding.CodePage);
			Assert.False(detected.HasByteOrderMark);
			Assert.Equal("{1}{}{Voilà, être.}", detected.Lines[0]);
			Assert.Equal(1252, forced.Encoding.CodePage);
			Assert.NotEqual(detected.Lines[0], forced.Lines[0]);
			Assert.True(forced.UsedFallback);
		}

		[Fact]
		public void Text_that_is_not_utf8_is_read_with_the_code_page_given()
		{
			Byte[] french = TextEncodings.FromCodePage(1252).GetBytes("{1}{}{Voilà, être.}");
			Byte[] russian = TextEncodings.FromCodePage(1251).GetBytes("{1}{}{Привет}");

			DecodedMessageFile western = MessageFileDecoder.Decode(french, TextEncodings.FromCodePage(1252), true);
			DecodedMessageFile cyrillic = MessageFileDecoder.Decode(russian, TextEncodings.FromCodePage(1251), true);
			DecodedMessageFile wrong = MessageFileDecoder.Decode(russian, TextEncodings.FromCodePage(1252), true);

			Assert.Equal("{1}{}{Voilà, être.}", western.Lines[0]);
			Assert.True(western.UsedFallback);
			Assert.Equal("{1}{}{Привет}", cyrillic.Lines[0]);
			Assert.NotEqual("{1}{}{Привет}", wrong.Lines[0]);
		}

		[Fact]
		public void Plain_ascii_is_not_a_use_of_the_fallback()
		{
			DecodedMessageFile file = MessageFileDecoder.Decode(Encoding.ASCII.GetBytes("{1}{}{Hello.}"), TextEncodings.Ansi, true);

			Assert.False(file.UsedFallback);
			Assert.False(file.HasSuspiciousCharacters);
		}

		[Theory]
		[InlineData("a\r\nb\r\n", "\r\n", true, 2)]
		[InlineData("a\nb\n", "\n", true, 2)]
		[InlineData("a\r\nb", "\r\n", false, 2)]
		[InlineData("a\nb\r\nc\nd", "\n", false, 4)]
		[InlineData("", "\r\n", false, 0)]
		[InlineData("\r\n", "\r\n", true, 1)]
		public void The_line_breaks_and_the_final_break_of_the_file_are_remembered(String text, String newLine, Boolean endsWithNewLine, Int32 lines)
		{
			DecodedMessageFile file = MessageFileDecoder.Decode(Encoding.ASCII.GetBytes(text), TextEncodings.Ansi, true);

			Assert.Equal(newLine, file.NewLine);
			Assert.Equal(endsWithNewLine, file.EndsWithNewLine);
			Assert.Equal(lines, file.Lines.Count);
			Assert.DoesNotContain(file.Lines, line => line.Contains('\r', StringComparison.Ordinal) || line.Contains('\n', StringComparison.Ordinal));
		}

		[Fact]
		public void Undefined_bytes_of_the_code_page_are_suspicious()
		{
			Byte[] bytes = TextEncodings.FromCodePage(1252).GetBytes("{1}{}{abc}").Concat(new Byte[] { 0x81 }).ToArray();

			DecodedMessageFile file = MessageFileDecoder.Decode(bytes, TextEncodings.FromCodePage(1252), true);

			Assert.True(file.HasSuspiciousCharacters);
		}
	}
}