namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.TestSupport;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The project lifecycle: open, close, create and save the settings file, recent projects, and the legacy solution folder.</summary>
	public sealed class ProjectServiceTests : IDisposable
	{
		private readonly TestFolder _root = new TestFolder("project-service");
		private readonly SettingsService _settings;
		private readonly List<ProjectService> _services = [];

		public ProjectServiceTests()
		{
			this._settings = new SettingsService(
				AppPaths.FromBaseDirectory(this._root.Combine("app")),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
			this._settings.Load();
		}

		public void Dispose()
		{
			foreach (ProjectService service in this._services)
			{
				service.Dispose();
			}

			this._settings.Dispose();
			this._root.Dispose();
		}

		private ProjectService CreateService()
		{
			ProjectService service = new ProjectService(this._settings, NullLogger<ProjectService>.Instance, ProjectFileWatchOptions.Disabled);
			this._services.Add(service);
			return service;
		}

		private String MakeFolder(String name, String? projectFile = null)
		{
			String folder = this._root.Combine(name);
			Directory.CreateDirectory(folder);
			if (projectFile is not null)
			{
				File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), projectFile);
			}

			return folder;
		}

		[Fact]
		public void WithNoProject_NothingChanges_AndTheGlobalSettingsAreInEffect()
		{
			ProjectService service = this.CreateService();

			Assert.False(service.IsOpen);
			Assert.Null(service.Folder);
			Assert.Equal(String.Empty, service.DisplayName);
			Assert.Null(service.Settings);
			Assert.False(service.CanCreateSettings);
			Assert.Same(this._settings.Settings, this._settings.Effective.Settings);
		}

		[Fact]
		public void TheLegacySolutionFolder_IsAProjectFolderWithoutASettingsFile()
		{
			String folder = this.MakeFolder("legacy");
			this._settings.Update(settings => settings.Paths.SolutionFolder = folder);

			ProjectService service = this.CreateService();

			Assert.True(service.IsOpen);
			Assert.Equal(folder, service.Folder);
			Assert.Equal("legacy", service.DisplayName);
			Assert.Equal(ProjectFileStatus.Missing, service.FileStatus);
			Assert.Null(service.Settings);
			Assert.True(service.CanCreateSettings);
			Assert.Same(this._settings.Settings, this._settings.Effective.Settings);
			Assert.Equal(folder, this._settings.Effective.ProjectFolder);
		}

		[Fact]
		public void Open_ReadsTheProjectFile_AndItsOverridesTakeEffect()
		{
			String folder = this.MakeFolder("mod", "{ \"name\": \"My Mod\", \"outputDirectory\": \"out\", \"optimization\": \"full\", \"defines\": [ \"MODDEF\" ] }");
			this._settings.Update(settings => settings.Compiler.Optimization = OptimizationLevel.None);
			ProjectService service = this.CreateService();
			Int32 changed = 0;
			service.ProjectChanged += (_, _) => changed++;

			Assert.True(service.Open(folder));

			Assert.Equal(folder, this._settings.Settings.Paths.SolutionFolder);
			Assert.Equal("My Mod", service.DisplayName);
			Assert.Equal(ProjectFileStatus.Loaded, service.FileStatus);
			Assert.Equal(Path.Combine(folder, "out"), this._settings.Effective.Settings.Paths.OutputDirectory);
			Assert.Equal(OptimizationLevel.Full, this._settings.Effective.Settings.Compiler.Optimization);
			Assert.Equal(OptimizationLevel.None, this._settings.Settings.Compiler.Optimization);
			Assert.Contains("MODDEF", this._settings.Effective.DefineChoices);
			Assert.True(changed >= 1);
		}

		[Fact]
		public void Close_GivesTheGlobalSettingsBack()
		{
			String folder = this.MakeFolder("mod", "{ \"shortCircuit\": true }");
			ProjectService service = this.CreateService();
			service.Open(folder);
			Assert.True(this._settings.Effective.Settings.Compiler.ShortCircuit);

			service.Close();

			Assert.False(service.IsOpen);
			Assert.Null(this._settings.Settings.Paths.SolutionFolder);
			Assert.False(this._settings.Effective.Settings.Compiler.ShortCircuit);
			Assert.Same(this._settings.Settings, this._settings.Effective.Settings);
			Assert.Null(this._settings.ActiveProject);
		}

		[Fact]
		public void OpeningAFolderThatDoesNotExist_ChangesNothing()
		{
			String folder = this.MakeFolder("mod");
			ProjectService service = this.CreateService();
			service.Open(folder);

			Assert.False(service.Open(this._root.Combine("missing")));
			Assert.False(service.Open("a<b>:|c"));

			Assert.Equal(folder, service.Folder);
		}

		[Fact]
		public void TheProjectFollowsTheSolutionFolderWhoeverChangesIt()
		{
			String first = this.MakeFolder("first", "{ \"name\": \"First\" }");
			String second = this.MakeFolder("second", "{ \"name\": \"Second\", \"debugInfo\": false }");
			ProjectService service = this.CreateService();
			Int32 changed = 0;
			service.ProjectChanged += (_, _) => changed++;

			this._settings.Update(settings => settings.Paths.SolutionFolder = first);
			Assert.Equal("First", service.DisplayName);
			this._settings.Update(settings => settings.Paths.SolutionFolder = second);

			Assert.Equal("Second", service.DisplayName);
			Assert.False(this._settings.Effective.Settings.Compiler.DebugInfo);
			Assert.Equal(2, changed);
		}

		[Fact]
		public void UnrelatedSettingChanges_DoNotReloadTheProject()
		{
			String folder = this.MakeFolder("mod", "{ \"name\": \"Mod\" }");
			ProjectService service = this.CreateService();
			service.Open(folder);
			Int32 changed = 0;
			service.ProjectChanged += (_, _) => changed++;

			this._settings.Update(settings => settings.Editor.TabSize = 6);

			Assert.Equal(0, changed);
			Assert.Equal(6, this._settings.Effective.Settings.Editor.TabSize);
		}

		[Fact]
		public void GlobalChanges_ReachTheOptionsTheProjectInherits()
		{
			String folder = this.MakeFolder("mod", "{ \"optimization\": \"full\" }");
			ProjectService service = this.CreateService();
			service.Open(folder);

			this._settings.Update(settings =>
			{
				settings.Compiler.Optimization = OptimizationLevel.None;
				settings.Compiler.ShowWarnings = false;
			});

			Assert.Equal(OptimizationLevel.Full, this._settings.Effective.Settings.Compiler.Optimization);
			Assert.False(this._settings.Effective.Settings.Compiler.ShowWarnings);
		}

		[Fact]
		public void CreateSettings_WritesTheFileWithTheValuesInEffect()
		{
			String folder = this.MakeFolder("mod");
			this._settings.Update(settings =>
			{
				settings.Paths.OutputDirectory = Path.Combine(folder, "build");
				settings.Compiler.Preprocessor = PreprocessorKind.Watcom;
				settings.Compiler.ShortCircuit = true;
			});
			ProjectService service = this.CreateService();
			service.Open(folder);

			Assert.True(service.CreateSettings());

			String json = File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName));
			Assert.Contains("\"outputDirectory\": \"build\"", json, StringComparison.Ordinal);
			Assert.Contains("\"preprocessor\": \"watcom\"", json, StringComparison.Ordinal);
			Assert.Contains("\"shortCircuit\": true", json, StringComparison.Ordinal);
			Assert.NotNull(service.Settings);
			Assert.False(service.CanCreateSettings);
			Assert.False(service.CreateSettings());
		}

		[Fact]
		public void SaveSettings_AppliesAtOnce_KeepsUnknownProperties_AndNeverTouchesTheGlobalFile()
		{
			String folder = this.MakeFolder("mod", "{ \"futureThing\": 5 }");
			ProjectService service = this.CreateService();
			service.Open(folder);
			Int32 changed = 0;
			service.ProjectChanged += (_, _) => changed++;
			String globalSettings = this._settings.Settings.Compiler.Optimization.ToString();

			ProjectSettings edited = service.Settings!.Clone();
			edited.Optimization = OptimizationLevel.Experimental;
			edited.Name = "Edited";
			Assert.True(service.SaveSettings(edited));

			Assert.Equal(1, changed);
			Assert.Equal(OptimizationLevel.Experimental, this._settings.Effective.Settings.Compiler.Optimization);
			Assert.Equal("Edited", service.DisplayName);
			Assert.Equal(globalSettings, this._settings.Settings.Compiler.Optimization.ToString());
			String json = File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName));
			Assert.Contains("futureThing", json, StringComparison.Ordinal);
			Assert.Contains("\"optimization\": \"experimental\"", json, StringComparison.Ordinal);
			Assert.False(File.Exists(this._settings.Paths.SettingsFile));
		}

		[Fact]
		public void SaveSettings_WithoutAProject_DoesNothing()
		{
			ProjectService service = this.CreateService();

			Assert.False(service.SaveSettings(new ProjectSettings()));
		}

		[Fact]
		public void ADamagedFile_IsReported_TheGlobalSettingsAreUsed_AndANewFileReplacesIt()
		{
			String folder = this.MakeFolder("mod", "{ not json");
			ProjectService service = this.CreateService();
			List<SettingsProblem> problems = [];
			service.ProblemRaised += (_, problem) => problems.Add(problem);

			service.Open(folder);

			SettingsProblem problem = Assert.Single(problems);
			Assert.Equal(SettingsProblemKind.Damaged, problem.Kind);
			Assert.Equal(Path.Combine(folder, ProjectSettings.FileName), problem.Path);
			Assert.Equal(ProjectFileStatus.Damaged, service.FileStatus);
			Assert.Null(service.Settings);
			Assert.Same(this._settings.Settings, this._settings.Effective.Settings);
			Assert.True(File.Exists(Path.Combine(folder, ProjectSettings.FileName + ".corrupt")));
			Assert.True(service.CanCreateSettings);

			Assert.True(service.CreateSettings());
			Assert.Equal(ProjectFileStatus.Loaded, service.FileStatus);
			Assert.Equal("{ not json", File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName + ".corrupt")));
		}

		[Fact]
		public void AnUnreadableFile_IsNeverOverwritten()
		{
			String folder = this.MakeFolder("mod", "{ \"name\": \"Keep\" }");
			String file = Path.Combine(folder, ProjectSettings.FileName);
			ProjectService service = this.CreateService();
			List<SettingsProblem> problems = [];
			service.ProblemRaised += (_, problem) => problems.Add(problem);

			using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				service.Open(folder);
				Assert.Equal(ProjectFileStatus.Unreadable, service.FileStatus);
				Assert.False(service.CanCreateSettings);
				Assert.False(service.CreateSettings());
				Assert.False(service.SaveSettings(new ProjectSettings { Name = "Overwritten" }));
			}

			Assert.Contains(problems, problem => problem.Kind == SettingsProblemKind.Unreadable);
			Assert.Equal("{ \"name\": \"Keep\" }", File.ReadAllText(file));
		}

		[Fact]
		public void AFileThatCannotBeWritten_RaisesASaveProblem_AndKeepsTheProjectAsItWas()
		{
			String folder = this.MakeFolder("mod", "{ \"name\": \"Before\" }");
			String file = Path.Combine(folder, ProjectSettings.FileName);
			ProjectService service = this.CreateService();
			service.Open(folder);
			List<SettingsProblem> problems = [];
			service.ProblemRaised += (_, problem) => problems.Add(problem);

			using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				Assert.False(service.SaveSettings(new ProjectSettings { Name = "After" }));
			}

			Assert.Equal(SettingsProblemKind.SaveFailed, Assert.Single(problems).Kind);
			Assert.Equal("Before", service.DisplayName);
			Assert.Equal("{ \"name\": \"Before\" }", File.ReadAllText(file));
		}

		[Fact]
		public void RecentProjects_AreNewestFirst_LimitedToTen_AndAReopenedProjectMovesToTheTop()
		{
			ProjectService service = this.CreateService();
			List<String> folders = Enumerable.Range(1, 12).Select(index => this.MakeFolder("p" + index)).ToList();

			foreach (String folder in folders)
			{
				service.Open(folder);
			}

			Assert.Equal(RecentFiles.MaxProjects, service.RecentProjects.Count);
			Assert.Equal(folders[11], service.RecentProjects[0]);
			Assert.Equal(folders[2], service.RecentProjects[^1]);

			service.Open(folders[5]);
			Assert.Equal(folders[5], service.RecentProjects[0]);
			Assert.Equal(RecentFiles.MaxProjects, service.RecentProjects.Count);
			Assert.Equal(1, service.RecentProjects.Count(path => path == folders[5]));
		}

		[Fact]
		public void RecentProjects_CanBeRemovedAndCleared()
		{
			ProjectService service = this.CreateService();
			String a = this.MakeFolder("a");
			String b = this.MakeFolder("b");
			service.Open(a);
			service.Open(b);

			service.RemoveRecent(a.ToUpperInvariant());
			Assert.Equal(new[] { b }, service.RecentProjects.ToArray());

			service.ClearRecent();
			Assert.Empty(service.RecentProjects);
		}

		[Fact]
		public void RecentProjects_SurviveARestart()
		{
			String folder = this.MakeFolder("kept");
			this.CreateService().Open(folder);
			this._settings.Save();

			using SettingsService reloaded = new SettingsService(
				this._settings.Paths,
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
			reloaded.Load();
			using ProjectService service = new ProjectService(reloaded, NullLogger<ProjectService>.Instance, ProjectFileWatchOptions.Disabled);

			Assert.Contains(folder, service.RecentProjects);
			Assert.Equal(folder, service.Folder);
		}

		[Fact]
		public void TheProjectIsFoundFromAScriptPath()
		{
			String folder = this.MakeFolder("mod", "{}");
			String script = this._root.Write(@"mod\scripts\a.ssl", "x");
			ProjectService service = this.CreateService();

			Assert.Equal(folder, service.FindProjectFolder(script));
			Assert.Null(service.FindProjectFolder(this._root.Write(@"elsewhere\b.ssl", "x")));
		}

		[Fact]
		public void Reload_ReadsAFileThatWasEditedByHand()
		{
			String folder = this.MakeFolder("mod", "{ \"name\": \"One\" }");
			ProjectService service = this.CreateService();
			service.Open(folder);

			File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Two\" }");
			service.Reload();

			Assert.Equal("Two", service.DisplayName);
		}

		[Fact]
		public void TheProjectsOfTwoServicesDoNotInterfere_WhenTheFolderIsTheSame()
		{
			String folder = this.MakeFolder("mod", "{ \"shortCircuit\": true }");
			ProjectService service = this.CreateService();

			service.Open(folder);
			service.Open(folder);

			Assert.True(this._settings.Effective.Settings.Compiler.ShortCircuit);
			Assert.Single(service.RecentProjects);
		}
	}
}