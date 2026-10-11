namespace FalloutScriptStudio.Core.Tests
{
	using System;
	using FalloutScriptStudio.Core.IO;
	using Xunit;

	public class TextEncodingsTests
	{
		[Fact]
		public void Ansi_IsNotUtf8()
		{
			Assert.NotEqual(65001, TextEncodings.Ansi.CodePage);
		}

		[Fact]
		public void Oem866_RoundTripsCyrillic()
		{
			Byte[] bytes = TextEncodings.Oem866.GetBytes("Привет");
			Assert.Equal("Привет", TextEncodings.Oem866.GetString(bytes));
		}
	}
}

