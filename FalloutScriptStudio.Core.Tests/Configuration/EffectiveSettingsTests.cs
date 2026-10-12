namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>Every option a project can override follows the override, and falls back to the global value when the project says nothing.</summary>
	public sealed class EffectiveSettingsTests
	{
		private static AppSettings CreateGlobal()
		{
			AppSettings global = new AppSettings();
			global.Paths.HeadersDirectory = @"C:\global\headers";
			global.Paths.ScriptsHeaderFile = @"C:\global\headers\SCRIPTS.H";
			global.Paths.OutputDirectory = @"C:\global\game\scripts";
			global.Paths.MessageSearchDirectories = new List<String> { @"C:\global\msg" };
			global.Compiler.CompileToSourceDirectory = true;
			global.Compiler.SearchIncludePath = true;
			global.Compiler.Preprocessor = PreprocessorKind.Mcpp;
			global.Compiler.PreprocessorDefine = "GLOBAL";
			global.Compiler.Optimization = OptimizationLevel.Basic;
			global.Compiler.ShortCircuit = false;
			global.Compiler.BackwardCompatibility = false;
			global.Compiler.ShowWarnings = true;
			global.Compiler.DebugInfo = true;
			return global;
		}

		private static EffectiveSettings Create(AppSettings global, ProjectSettings? project, PreprocessorDefines? defines = null)
		{
			return new EffectiveSettings(global, defines ?? new PreprocessorDefines(), project is null ? null : @"C:\project", project);
		}

		[Fact]
		public void WithoutAProjectTheGlobalSettingsThemselvesAreInEffect()
		{
			AppSettings global = CreateGlobal();

			EffectiveSettings effective = Create(global, null);

			Assert.Same(global, effective.Settings);
			Assert.Null(effective.Project);
			Assert.Null(effective.ScriptsListFile);
		}

		[Fact]
		public void AProjectThatOverridesNothing_GivesTheGlobalValues()
		{
			AppSettings global = CreateGlobal();

			AppSettings effective = Create(global, new ProjectSettings()).Settings;

			Assert.Equal(@"C:\global\headers", effective.Paths.HeadersDirectory);
			Assert.Equal(@"C:\global\headers\SCRIPTS.H", effective.Paths.ScriptsHeaderFile);
			Assert.Equal(@"C:\global\game\scripts", effective.Paths.OutputDirectory);
			Assert.Equal(new[] { @"C:\global\msg" }, effective.Paths.MessageSearchDirectories);
			Assert.True(effective.Compiler.CompileToSourceDirectory);
			Assert.True(effective.Compiler.SearchIncludePath);
			Assert.Equal(PreprocessorKind.Mcpp, effective.Compiler.Preprocessor);
			Assert.Equal("GLOBAL", effective.Compiler.PreprocessorDefine);
			Assert.Equal(OptimizationLevel.Basic, effective.Compiler.Optimization);
			Assert.False(effective.Compiler.ShortCircuit);
			Assert.False(effective.Compiler.BackwardCompatibility);
			Assert.True(effective.Compiler.ShowWarnings);
			Assert.True(effective.Compiler.DebugInfo);
		}

		[Fact]
		public void EveryOverrideWins()
		{
			AppSettings global = CreateGlobal();
			ProjectSettings project = new ProjectSettings
			{
				HeadersDirectory = @"D:\mod\headers",
				ScriptsHeaderFile = @"D:\mod\headers\scripts.h",
				OutputDirectory = @"D:\mod\out",
				MessageSearchDirectories = new List<String> { @"D:\mod\msg", @"D:\mod\msg2" },
				CompileToSourceDirectory = false,
				SearchIncludePath = false,
				Preprocessor = PreprocessorKind.Watcom,
				PreprocessorDefine = "MOD",
				Optimization = OptimizationLevel.Full,
				ShortCircuit = true,
				BackwardCompatibility = true,
				ShowWarnings = false,
				DebugInfo = false,
			};

			AppSettings effective = Create(global, project).Settings;

			Assert.Equal(@"D:\mod\headers", effective.Paths.HeadersDirectory);
			Assert.Equal(@"D:\mod\headers\scripts.h", effective.Paths.ScriptsHeaderFile);
			Assert.Equal(@"D:\mod\out", effective.Paths.OutputDirectory);
			Assert.Equal(new[] { @"D:\mod\msg", @"D:\mod\msg2" }, effective.Paths.MessageSearchDirectories);
			Assert.False(effective.Compiler.CompileToSourceDirectory);
			Assert.False(effective.Compiler.SearchIncludePath);
			Assert.Equal(PreprocessorKind.Watcom, effective.Compiler.Preprocessor);
			Assert.Equal("MOD", effective.Compiler.PreprocessorDefine);
			Assert.Equal(OptimizationLevel.Full, effective.Compiler.Optimization);
			Assert.True(effective.Compiler.ShortCircuit);
			Assert.True(effective.Compiler.BackwardCompatibility);
			Assert.False(effective.Compiler.ShowWarnings);
			Assert.False(effective.Compiler.DebugInfo);
		}

		[Fact]
		public void AnOverrideNeverChangesTheGlobalSettings()
		{
			AppSettings global = CreateGlobal();
			ProjectSettings project = new ProjectSettings { HeadersDirectory = "x", ShortCircuit = true, MessageSearchDirectories = new List<String>() };

			_ = Create(global, project).Settings;

			Assert.Equal(@"C:\global\headers", global.Paths.HeadersDirectory);
			Assert.False(global.Compiler.ShortCircuit);
			Assert.Single(global.Paths.MessageSearchDirectories);
		}

		[Fact]
		public void ABlankOverrideMeansNotSet_EvenWhenTheGlobalSettingIsSet()
		{
			ProjectSettings project = new ProjectSettings { HeadersDirectory = String.Empty, ScriptsHeaderFile = " ", OutputDirectory = String.Empty, PreprocessorDefine = String.Empty };

			AppSettings effective = Create(CreateGlobal(), project).Settings;

			Assert.Null(effective.Paths.HeadersDirectory);
			Assert.Null(effective.Paths.ScriptsHeaderFile);
			Assert.Null(effective.Paths.OutputDirectory);
			Assert.Null(effective.Compiler.PreprocessorDefine);
		}

		[Fact]
		public void AnEmptyMessageFolderListOverrideReplacesTheGlobalFolders()
		{
			ProjectSettings project = new ProjectSettings { MessageSearchDirectories = new List<String>() };

			Assert.Empty(Create(CreateGlobal(), project).Settings.Paths.MessageSearchDirectories);
		}

		[Fact]
		public void TheOptionsThatAreNotPerProjectComeFromTheGlobalSettings()
		{
			AppSettings global = CreateGlobal();
			global.Compiler.UseUserCommand = true;
			global.Compiler.Decompiler = DecompilerKind.ExternalInt2Ssl;
			global.Editor.TabSize = 7;
			global.Messages.Language = "german";
			global.Paths.SolutionFolder = @"C:\project";

			AppSettings effective = Create(global, new ProjectSettings { ShortCircuit = true }).Settings;

			Assert.True(effective.Compiler.UseUserCommand);
			Assert.Equal(DecompilerKind.ExternalInt2Ssl, effective.Compiler.Decompiler);
			Assert.Equal(7, effective.Editor.TabSize);
			Assert.Equal("german", effective.Messages.Language);
			Assert.Equal(@"C:\project", effective.Paths.SolutionFolder);
		}

		[Fact]
		public void TheDefineChoices_AddTheProjectDefinesToTheGlobalOnes_WithoutDuplicates()
		{
			PreprocessorDefines defines = new PreprocessorDefines { Items = new List<String> { "G1", "SHARED" } };
			ProjectSettings project = new ProjectSettings { Defines = new List<String> { "P1", "SHARED", "DEBUG" } };

			Assert.Equal(new[] { "DEBUG", "RELEASE", "G1", "SHARED", "P1" }, Create(CreateGlobal(), project, defines).DefineChoices.ToArray());
			Assert.Equal(new[] { "DEBUG", "RELEASE", "G1", "SHARED" }, Create(CreateGlobal(), null, defines).DefineChoices.ToArray());
		}

		[Fact]
		public void TheScriptsListFileComesFromTheProjectOnly()
		{
			Assert.Equal(@"D:\mod\scripts.lst", Create(CreateGlobal(), new ProjectSettings { ScriptsListFile = @"D:\mod\scripts.lst" }).ScriptsListFile);
			Assert.Null(Create(CreateGlobal(), new ProjectSettings { ScriptsListFile = " " }).ScriptsListFile);
			Assert.Null(Create(CreateGlobal(), new ProjectSettings()).ScriptsListFile);
		}

		[Fact]
		public void CompilerOptions_FollowTheProject_AndFallBackToTheGlobalValues()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(@"C:\app");
			ProjectSettings project = new ProjectSettings
			{
				OutputDirectory = @"D:\mod\out",
				HeadersDirectory = @"D:\mod\headers",
				Preprocessor = PreprocessorKind.Builtin,
				PreprocessorDefine = "MOD",
				Optimization = OptimizationLevel.None,
				ShortCircuit = true,
				BackwardCompatibility = true,
				ShowWarnings = false,
				DebugInfo = false,
				CompileToSourceDirectory = false,
				SearchIncludePath = false,
			};

			CompilerOptions overridden = Create(CreateGlobal(), project).ToCompilerOptions(paths);
			CompilerOptions inherited = Create(CreateGlobal(), new ProjectSettings()).ToCompilerOptions(paths);

			Assert.Equal(@"D:\mod\out", overridden.OutputDirectory);
			Assert.Equal(@"D:\mod\headers", overridden.HeadersDirectory);
			Assert.Equal(PreprocessorKind.Builtin, overridden.Preprocessor);
			Assert.Equal("MOD", overridden.PreprocessorDefine);
			Assert.Equal(OptimizationLevel.None, overridden.Optimization);
			Assert.True(overridden.ShortCircuit);
			Assert.True(overridden.BackwardCompatibility);
			Assert.False(overridden.ShowWarnings);
			Assert.False(overridden.DebugInfo);
			Assert.False(overridden.CompileToSourceDirectory);
			Assert.False(overridden.SearchIncludePath);

			Assert.Equal(@"C:\global\game\scripts", inherited.OutputDirectory);
			Assert.Equal(@"C:\global\headers", inherited.HeadersDirectory);
			Assert.Equal(PreprocessorKind.Mcpp, inherited.Preprocessor);
			Assert.Equal("GLOBAL", inherited.PreprocessorDefine);
			Assert.Equal(OptimizationLevel.Basic, inherited.Optimization);
			Assert.True(inherited.CompileToSourceDirectory);
			Assert.Equal(paths.ProgramDirectory, overridden.ProgramDirectory);
		}

		[Fact]
		public void TheIncludeHeadersFolder_FollowsTheProject()
		{
			AppSettings global = CreateGlobal();

			Assert.Equal(@"C:\global\headers", Create(global, null).GetIncludeHeadersDirectory());
			Assert.Equal(@"D:\mod\headers", Create(global, new ProjectSettings { HeadersDirectory = @"D:\mod\headers" }).GetIncludeHeadersDirectory());
			Assert.Null(Create(global, new ProjectSettings { SearchIncludePath = false }).GetIncludeHeadersDirectory());
			Assert.Null(Create(global, new ProjectSettings { ScriptsHeaderFile = String.Empty }).GetIncludeHeadersDirectory());
		}

		[Fact]
		public void TheIncludeResolver_FindsTheHeadersOfTheProjectFolder()
		{
			using TestFolder root = new TestFolder("effective-include");
			String globalHeader = root.Write(@"global\common.h", "// global");
			String projectHeader = root.Write(@"mod\common.h", "// mod");
			AppSettings global = CreateGlobal();
			global.Paths.HeadersDirectory = Path.GetDirectoryName(globalHeader);
			AppPaths paths = AppPaths.FromBaseDirectory(root.Combine("app"));
			ProjectSettings project = new ProjectSettings { HeadersDirectory = Path.GetDirectoryName(projectHeader) };

			Assert.Equal(globalHeader, Create(global, null).ToIncludeResolver(paths).Resolve("common.h", null));
			Assert.Equal(projectHeader, Create(global, project).ToIncludeResolver(paths).Resolve("common.h", null));
		}

		[Fact]
		public void TheMessageFileOptions_FollowTheProject()
		{
			AppSettings global = CreateGlobal();
			ProjectSettings project = new ProjectSettings { OutputDirectory = @"D:\mod\out", MessageSearchDirectories = new List<String> { @"D:\mod\msg" } };

			MessageFileOptions overridden = Create(global, project).ToMessageFileOptions();
			MessageFileOptions inherited = Create(global, new ProjectSettings()).ToMessageFileOptions();

			Assert.Equal(@"D:\mod\out", overridden.OutputDirectory);
			Assert.Equal(new[] { @"D:\mod\msg" }, overridden.SearchDirectories);
			Assert.Equal(@"C:\global\game\scripts", inherited.OutputDirectory);
			Assert.Equal(new[] { @"C:\global\msg" }, inherited.SearchDirectories);
		}

		[Fact]
		public void CreateFrom_CopiesTheValuesInEffect()
		{
			AppSettings effective = Create(CreateGlobal(), new ProjectSettings { ShortCircuit = true, PreprocessorDefine = "MOD" }).Settings;

			ProjectSettings created = ProjectSettings.CreateFrom(effective, "Mod");

			Assert.Equal("Mod", created.Name);
			Assert.Equal(@"C:\global\headers", created.HeadersDirectory);
			Assert.Equal(@"C:\global\game\scripts", created.OutputDirectory);
			Assert.Equal("MOD", created.PreprocessorDefine);
			Assert.True(created.ShortCircuit);
			Assert.Equal(PreprocessorKind.Mcpp, created.Preprocessor);
			Assert.Equal(new[] { @"C:\global\msg" }, created.MessageSearchDirectories);
			Assert.Null(created.ScriptsListFile);
		}

		[Fact]
		public void TheSettingsServiceHandsOutTheEffectiveSettingsOfTheActiveProject()
		{
			using TestFolder root = new TestFolder("effective-service");
			SettingsService service = new SettingsService(
				AppPaths.FromBaseDirectory(root.Path),
				new LegacySettingsImporter(Microsoft.Extensions.Logging.Abstractions.NullLogger<LegacySettingsImporter>.Instance),
				Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsService>.Instance);
			service.Settings.Compiler.Optimization = OptimizationLevel.Basic;
			Int32 changes = 0;
			service.SettingsChanged += (_, _) => changes++;

			Assert.Same(service.Settings, service.Effective.Settings);

			service.SetActiveProject(@"C:\project", new ProjectSettings { Optimization = OptimizationLevel.Full });

			Assert.Equal(1, changes);
			Assert.Equal(OptimizationLevel.Full, service.Effective.Settings.Compiler.Optimization);
			Assert.Equal(OptimizationLevel.Basic, service.Settings.Compiler.Optimization);
			Assert.Equal(@"C:\project", service.Effective.ProjectFolder);

			service.Update(settings => settings.Compiler.ShortCircuit = true);
			Assert.True(service.Effective.Settings.Compiler.ShortCircuit);

			service.SetActiveProject(null, null);
			Assert.Equal(OptimizationLevel.Basic, service.Effective.Settings.Compiler.Optimization);
			Assert.Null(service.Effective.ProjectFolder);
		}
	}
}
