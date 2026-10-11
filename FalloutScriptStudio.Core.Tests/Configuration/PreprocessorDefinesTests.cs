namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class PreprocessorDefinesTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private AppPaths Paths => AppPaths.FromBaseDirectory(this._workspace.Root);

		private SettingsService CreateService()
		{
			return new SettingsService(
				this.Paths,
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
		}

		[Theory]
		[InlineData("DEBUG", true)]
		[InlineData("FOO=1", true)]
		[InlineData("  TEST  ", true)]
		[InlineData("", false)]
		[InlineData("   ", false)]
		[InlineData(null, false)]
		[InlineData("<Not Preset>", false)]
		[InlineData("<none>", false)]
		[InlineData("A B", false)]
		public void IsValidDefinition_RejectsBlanksPlaceholdersAndWhiteSpace(String? text, Boolean expected)
		{
			Assert.Equal(expected, PreprocessorDefines.IsValidDefinition(text));
		}

		[Fact]
		public void Sanitize_DropsPlaceholdersBlanksDuplicatesAndTheBuiltInOnes()
		{
			PreprocessorDefines defines = new PreprocessorDefines();
			defines.Items.AddRange(new[] { "<Not Preset>", "TEST", " ", "DEBUG", "TEST", " ALPHA ", "RELEASE" });

			defines.Sanitize();

			Assert.Equal(new[] { "TEST", "ALPHA" }, defines.Items);
		}

		[Fact]
		public void GetChoices_ListsTheBuiltInDefinitionsFirstWithoutDuplicates()
		{
			PreprocessorDefines defines = new PreprocessorDefines();
			defines.Items.AddRange(new[] { "TEST", "DEBUG", "<Not Preset>", "TEST" });

			Assert.Equal(new[] { "DEBUG", "RELEASE", "TEST" }, defines.GetChoices());
		}

		[Fact]
		public void LegacyIniFile_EveryLineIsAUserDefinitionAndNoPlaceholderIsImported()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.LegacyPreprocessorDefinesFile, "<Not Preset>\r\nMY_DEFINE\r\n\r\nDEBUG\r\nMY_DEFINE\r\nOTHER=2\r\n");

			SettingsService service = this.CreateService();
			service.Load();

			Assert.Equal(new[] { "MY_DEFINE", "OTHER=2" }, service.PreprocessorDefines.Items);
			Assert.Equal(new[] { "DEBUG", "RELEASE", "MY_DEFINE", "OTHER=2" }, service.PreprocessorDefines.GetChoices());
		}

		[Fact]
		public void LegacyIniFile_TheFirstLineIsNotDroppedBecauseItIsARealDefinition()
		{
			// In the legacy editor "<Not Preset>", DEBUG and RELEASE were items of the combo box itself: PreprocDefine.ini only held user lines.
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.LegacyPreprocessorDefinesFile, "FIRST\r\nSECOND\r\n");

			SettingsService service = this.CreateService();
			service.Load();

			Assert.Equal(new[] { "FIRST", "SECOND" }, service.PreprocessorDefines.Items);
		}

		[Fact]
		public void Load_SanitisesAnExistingJsonFileThatAlreadyContainsTheLabel()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.PreprocessorDefinesFile, "{ \"items\": [ \"<Not Preset>\", \"DEBUG\", \"KEEP\", \"\", \"KEEP\" ] }");

			SettingsService service = this.CreateService();
			service.Load();

			Assert.Equal(new[] { "KEEP" }, service.PreprocessorDefines.Items);
		}

		[Fact]
		public void Normalize_ClearsAPlaceholderStoredAsTheSelectedDefinition()
		{
			AppSettings settings = new AppSettings();
			settings.Compiler.PreprocessorDefine = "<Not Preset>";
			settings.Normalize();
			Assert.Null(settings.Compiler.PreprocessorDefine);

			settings.Compiler.PreprocessorDefine = "  ";
			settings.Normalize();
			Assert.Null(settings.Compiler.PreprocessorDefine);

			settings.Compiler.PreprocessorDefine = "REL";
			settings.Normalize();
			Assert.Equal("REL", settings.Compiler.PreprocessorDefine);
		}

		[Fact]
		public void OldSettingsJsonWithTheRemovedNativeTextRenderingOptionStillLoads()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.SettingsFile, "{ \"editor\": { \"tabSize\": 6, \"nativeTextRendering\": false, \"showTips\": false }, \"compiler\": { \"optimization\": \"full\" } }");

			SettingsService service = this.CreateService();
			service.Load();

			Assert.Equal(6, service.Settings.Editor.TabSize);
			Assert.False(service.Settings.Editor.ShowTips);
			Assert.Equal(OptimizationLevel.Full, service.Settings.Compiler.Optimization);
			Assert.False(File.Exists(this.Paths.SettingsFile + ".corrupt"));
		}
	}
}
