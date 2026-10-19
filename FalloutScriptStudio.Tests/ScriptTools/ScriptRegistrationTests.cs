namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Services.ScriptTools.Registration;
	using Xunit;

	public sealed class ScriptRegistrationTests
	{
		[Theory]
		[InlineData("C:\\scripts\\hakunin.ssl", "hakunin.int")]
		[InlineData("C:\\scripts\\HAKUNIN.SSL", "HAKUNIN.int")]
		[InlineData("C:\\scripts\\eightchr.int", "eightchr.int")]
		public void Check_RegisterableScripts_AreAllowedWithTheCompiledName(String path, String expected)
		{
			ScriptRegistrationCheck check = ScriptRegistrationChecker.Check(path);

			Assert.Equal(ScriptRegistrationCheckKind.Allowed, check.Kind);
			Assert.Equal(expected, check.ScriptFileName);
		}

		[Fact]
		public void Check_UnsavedScript_IsRefused()
		{
			ScriptRegistrationCheck check = ScriptRegistrationChecker.Check(null);

			Assert.Equal(ScriptRegistrationCheckKind.Refused, check.Kind);
			Assert.Contains("unsaved", check.Message, StringComparison.Ordinal);
		}

		[Theory]
		[InlineData("C:\\scripts\\readme.txt")]
		[InlineData("C:\\scripts\\scripts.h")]
		public void Check_OtherFiles_AreRefused(String path)
		{
			Assert.Equal(ScriptRegistrationCheckKind.Refused, ScriptRegistrationChecker.Check(path).Kind);
		}

		[Fact]
		public void Check_LongName_IsRefused()
		{
			ScriptRegistrationCheck check = ScriptRegistrationChecker.Check("C:\\scripts\\ninechars.ssl");

			Assert.Equal(ScriptRegistrationCheckKind.Refused, check.Kind);
			Assert.Contains("8 characters", check.Message, StringComparison.Ordinal);
		}

		[Fact]
		public void Check_NameWithSpace_IsRefused()
		{
			Assert.Equal(ScriptRegistrationCheckKind.Refused, ScriptRegistrationChecker.Check("C:\\scripts\\my script.ssl").Kind);
		}

		[Theory]
		[InlineData("C:\\scripts\\glboom.ssl")]
		[InlineData("C:\\scripts\\GLBOOM.ssl")]
		public void Check_GlobalScript_NeedsConfirmation(String path)
		{
			ScriptRegistrationCheck check = ScriptRegistrationChecker.Check(path);

			Assert.Equal(ScriptRegistrationCheckKind.NeedsConfirmation, check.Kind);
			Assert.EndsWith(".int", check.ScriptFileName, StringComparison.Ordinal);
		}

		[Theory]
		[InlineData("abc.int", true)]
		[InlineData("abcdefgh.int", true)]
		[InlineData("abcdefghi.int", false)]
		[InlineData("", true)]
		public void IsValidListedName_LimitsTheLengthWithoutExtension(String name, Boolean expected)
		{
			Assert.Equal(expected, ScriptRegistrationChecker.IsValidListedName(name));
		}

		[Fact]
		public void Resolve_WithoutOutputDirectory_IsAnError()
		{
			ScriptRegistryPathResult result = ScriptRegistryPathResolver.Resolve(null, null, "english");

			Assert.Null(result.Paths);
			Assert.Equal(ScriptRegistryProblemKind.OutputDirectoryNotSet, Assert.Single(result.Problems).Kind);
			Assert.True(result.Problems[0].IsError);
		}

		[Fact]
		public void Resolve_WithoutList_IsAnError()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();

			ScriptRegistryPathResult result = ScriptRegistryPathResolver.Resolve(temp.Path, null, "english");

			Assert.Null(result.Paths);
			Assert.Equal(ScriptRegistryProblemKind.ScriptsListMissing, Assert.Single(result.Problems).Kind);
		}

		[Fact]
		public void Resolve_FindsTheFilesAndReportsMissingOnesAsWarnings()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String scripts = Path.GetDirectoryName(temp.Write("data/scripts/scripts.lst", String.Empty))!;
			String header = temp.Write("headers/scripts.h", String.Empty);

			ScriptRegistryPathResult withoutMessages = ScriptRegistryPathResolver.Resolve(scripts, header, "english");
			temp.Write("data/text/german/game/scrname.msg", String.Empty);
			ScriptRegistryPathResult german = ScriptRegistryPathResolver.Resolve(scripts, null, "german");

			Assert.NotNull(withoutMessages.Paths);
			Assert.Equal(header, withoutMessages.Paths.HeaderPath);
			Assert.Null(withoutMessages.Paths.NameMessagePath);
			Assert.Equal(ScriptRegistryProblemKind.NameMessageMissing, Assert.Single(withoutMessages.Problems).Kind);
			Assert.False(withoutMessages.Problems[0].IsError);
			Assert.NotNull(german.Paths);
			Assert.Equal(temp.Combine("data", "text", "german", "game", "scrname.msg"), german.Paths.NameMessagePath);
			Assert.Equal(ScriptRegistryProblemKind.HeaderNotSet, Assert.Single(german.Problems).Kind);
		}

		[Fact]
		public void Resolve_MissingHeaderFile_IsAWarningAndNoPath()
		{
			using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
			String scripts = Path.GetDirectoryName(temp.Write("data/scripts/scripts.lst", String.Empty))!;

			ScriptRegistryPathResult result = ScriptRegistryPathResolver.Resolve(scripts, temp.Combine("nope.h"), "english");

			Assert.NotNull(result.Paths);
			Assert.Null(result.Paths.HeaderPath);
			Assert.Contains(result.Problems, problem => problem.Kind == ScriptRegistryProblemKind.HeaderMissing);
		}

		[Fact]
		public void Find_ForwardWrapsAndStartsAfterTheCurrentCell()
		{
			List<ScriptListEntry> entries =
			[
				new ScriptListEntry { Script = "alpha.int", Description = "boss" },
				new ScriptListEntry { Script = "beta.int", Description = "Boss fight", GameName = "The BOSS" },
			];

			(Int32 Row, Int32 Column)? first = ScriptListFinder.Find(entries, " boss ", -1, 0, true);
			(Int32 Row, Int32 Column)? second = ScriptListFinder.Find(entries, "boss", first!.Value.Row, first.Value.Column, true);
			(Int32 Row, Int32 Column)? third = ScriptListFinder.Find(entries, "boss", second!.Value.Row, second.Value.Column, true);
			(Int32 Row, Int32 Column)? wrapped = ScriptListFinder.Find(entries, "boss", third!.Value.Row, third.Value.Column, true);

			Assert.Equal((0, 2), first);
			Assert.Equal((1, 2), second);
			Assert.Equal((1, 4), third);
			Assert.Equal((0, 2), wrapped);
		}

		[Fact]
		public void Find_BackwardGoesToThePreviousMatch()
		{
			List<ScriptListEntry> entries =
			[
				new ScriptListEntry { Script = "alpha.int" },
				new ScriptListEntry { Script = "alpha2.int" },
			];

			(Int32 Row, Int32 Column)? previous = ScriptListFinder.Find(entries, "alpha", 1, 1, false);
			(Int32 Row, Int32 Column)? fromStart = ScriptListFinder.Find(entries, "alpha", -1, 0, false);

			Assert.Equal((0, 1), previous);
			Assert.Equal((1, 1), fromStart);
		}

		[Fact]
		public void Find_SearchesTheNumbersAndLocalVariableCounts()
		{
			List<ScriptListEntry> entries = Enumerable.Range(0, 12).Select(i => new ScriptListEntry { Script = "s" + i + ".int", LocalVars = i == 5 ? 77 : 0 }).ToList();

			(Int32 Row, Int32 Column)? byNumber = ScriptListFinder.Find(entries, "12", -1, 0, true);
			(Int32 Row, Int32 Column)? byVars = ScriptListFinder.Find(entries, "77", -1, 0, true);

			Assert.Equal((11, 0), byNumber);
			Assert.Equal((5, 3), byVars);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("zzz")]
		public void Find_NothingToFind_ReturnsNull(String text)
		{
			List<ScriptListEntry> entries = [new ScriptListEntry { Script = "a.int" }];

			Assert.Null(ScriptListFinder.Find(entries, text, -1, 0, true));
		}
	}
}