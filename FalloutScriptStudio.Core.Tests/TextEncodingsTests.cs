namespace FalloutScriptStudio.Core.Tests
{
	using System;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using FalloutScriptStudio.Core.IO;
	using Xunit;

	public class TextEncodingsTests
	{
		[Fact]
		public void Ansi_IsNotUtf8()
		{
			Assert.NotEqual(65001, TextEncodings.Ansi.CodePage);
		}

		[Theory]
		[InlineData(1251, 1251)]
		[InlineData(1252, 1252)]
		[InlineData(932, 932)]
		[InlineData(0, 1252)]
		[InlineData(-1, 1252)]
		[InlineData(65001, 1252)]
		[InlineData(1200, 1252)]
		[InlineData(99999, 1252)]
		public void ResolveSystemEncoding_UsesTheReportedCodePage(Int32 reported, Int32 expected)
		{
			Assert.Equal(expected, TextEncodings.ResolveSystemEncoding(() => reported).CodePage);
		}

		[Fact]
		public void ResolveSystemEncoding_FallsBackWhenTheProbeFails()
		{
			Assert.Equal(1252, TextEncodings.ResolveSystemEncoding(() => throw new InvalidOperationException()).CodePage);
		}

		[Fact]
		public void Ansi_IgnoresTheRegionalFormatOfTheUser()
		{
			// The regional format (here Russian) says nothing about the ANSI code page of the system (GetACP).
			CultureInfo previous = CultureInfo.CurrentCulture;
			try
			{
				CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
				Assert.Equal(1252, TextEncodings.ResolveSystemEncoding(() => 1252).CodePage);
			}
			finally
			{
				CultureInfo.CurrentCulture = previous;
			}
		}

		[Fact]
		public void AnsiAndOem_AreTheCodePagesOfTheSystem()
		{
			if (!OperatingSystem.IsWindows())
			{
				return;
			}

			Assert.Equal((Int32)GetACP(), TextEncodings.Ansi.CodePage);
			Assert.Equal((Int32)GetOEMCP(), TextEncodings.Oem.CodePage);
		}

		[DllImport("kernel32.dll")]
		private static extern UInt32 GetACP();

		[DllImport("kernel32.dll")]
		private static extern UInt32 GetOEMCP();

		[Fact]
		public void Oem866_RoundTripsCyrillic()
		{
			Byte[] bytes = TextEncodings.Oem866.GetBytes("Привет");
			Assert.Equal("Привет", TextEncodings.Oem866.GetString(bytes));
		}
	}
}

