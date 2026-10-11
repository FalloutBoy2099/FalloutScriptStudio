namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using FalloutScriptStudio.Services.ScriptTools.Registration;
	using Xunit;

	public sealed class ScriptListEntryTests
	{
		private static readonly String Ha = ((Char)0x0425).ToString();

		[Fact]
		public void Parse_LegacyLine_ReadsAllParts()
		{
			ScriptListEntry entry = ScriptListEntry.Parse("hakunin.int      ; Hakunin's script; the old man     # local_vars=7");

			Assert.Equal("hakunin.int", entry.Script);
			Assert.Equal("Hakunin's script; the old man", entry.Description);
			Assert.Equal(7, entry.LocalVars);
		}

		[Fact]
		public void Parse_NewRegistrationLine_HasEmptyDescription()
		{
			ScriptListEntry entry = ScriptListEntry.Parse("newone.int; # local_vars=0");

			Assert.Equal("newone.int", entry.Script);
			Assert.Equal(String.Empty, entry.Description);
			Assert.Equal(0, entry.LocalVars);
		}

		[Fact]
		public void Parse_RowAddedByTheEditor_HasNoVariableCount()
		{
			ScriptListEntry entry = ScriptListEntry.Parse("none.int; # local_vars=");

			Assert.Equal("none.int", entry.Script);
			Assert.Equal(0, entry.LocalVars);
		}

		[Theory]
		[InlineData("", "", "", 0)]
		[InlineData("just_a_name.int", "just_a_name.int", "", 0)]
		[InlineData("a.int ; desc only", "a.int", "desc only", 0)]
		[InlineData("a.int # local_vars=4", "a.int", "", 4)]
		[InlineData("a.int # local_vars=4 ; trailing", "a.int", "", 4)]
		[InlineData("a.int ; d # local_vars=3x", "a.int", "d", 3)]
		public void Parse_BrokenLines_DoNotThrow(String line, String script, String description, Int32 vars)
		{
			ScriptListEntry entry = ScriptListEntry.Parse(line);

			Assert.Equal(script, entry.Script);
			Assert.Equal(description, entry.Description);
			Assert.Equal(vars, entry.LocalVars);
		}

		[Fact]
		public void ToListLine_UsesTheLegacyColumns()
		{
			ScriptListEntry entry = new ScriptListEntry { Script = "abc.int", Description = "Test", LocalVars = 5 };

			Assert.Equal("abc.int".PadRight(16) + "; " + "Test".PadRight(45) + " # local_vars=5", entry.ToListLine());
		}

		[Fact]
		public void ListLine_RoundTrips()
		{
			ScriptListEntry original = new ScriptListEntry { Script = "abc.int", Description = "Some; description", LocalVars = 12 };

			ScriptListEntry copy = ScriptListEntry.Parse(original.ToListLine());

			Assert.Equal(original.Script, copy.Script);
			Assert.Equal(original.Description, copy.Description);
			Assert.Equal(original.LocalVars, copy.LocalVars);
		}

		[Fact]
		public void ToMessageLine_UsesIdFrom101AndTheLegacyColumns()
		{
			ScriptListEntry entry = new ScriptListEntry { Script = "abc.int", Description = "Test", GameName = "Hakunin", Lip = "lip" };

			String line = entry.ToMessageLine(2, false);

			Assert.Equal("{103}{lip}{Hakunin}".PadRight(60) + "# " + "abc.int".PadRight(16) + "; Test", line);
		}

		[Fact]
		public void ToMessageLine_ReplacesTheCyrillicHaForCp866()
		{
			ScriptListEntry entry = new ScriptListEntry { Script = "a.int", GameName = Ha + "ey" };

			Assert.StartsWith("{101}{}{Xey}", entry.ToMessageLine(0, true), StringComparison.Ordinal);
			Assert.StartsWith("{101}{}{" + Ha + "ey}", entry.ToMessageLine(0, false), StringComparison.Ordinal);
		}
	}
}
