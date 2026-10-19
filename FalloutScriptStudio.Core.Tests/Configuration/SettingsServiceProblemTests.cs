namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>A damaged, unreadable or unsaveable settings file is reported to the application instead of being silently replaced by defaults.</summary>
	public sealed class SettingsServiceProblemTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private AppPaths Paths => AppPaths.FromBaseDirectory(this._workspace.Root);

		private SettingsService CreateService(Func<String, Byte[]>? readFile = null)
		{
			return new SettingsService(
				this.Paths,
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance)
			{
				ReadAttempts = 2,
				ReadRetryDelay = TimeSpan.Zero,
				ReadFile = readFile ?? File.ReadAllBytes,
			};
		}

		[Fact]
		public void A_clean_start_has_no_problem()
		{
			SettingsService service = this.CreateService();

			service.Load();

			Assert.Empty(service.Problems);
		}

		[Fact]
		public void A_damaged_file_is_reported_with_its_path_and_the_event_is_raised()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.SettingsFile, "{ this is not json");
			SettingsService service = this.CreateService();
			List<SettingsProblem> raised = [];
			service.ProblemRaised += (_, problem) => raised.Add(problem);

			service.Load();

			SettingsProblem problem = Assert.Single(service.Problems);
			Assert.Equal(new SettingsProblem(SettingsProblemKind.Damaged, this.Paths.SettingsFile), problem);
			Assert.Equal(new[] { problem }, raised);
		}

		[Fact]
		public void A_file_that_cannot_be_read_is_reported_and_left_untouched()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.RecentFilesFile, "[]");
			SettingsService service = this.CreateService(path => path == this.Paths.RecentFilesFile ? throw new IOException("locked by an antivirus") : File.ReadAllBytes(path));

			service.Load();

			SettingsProblem problem = Assert.Single(service.Problems);
			Assert.Equal(SettingsProblemKind.Unreadable, problem.Kind);
			Assert.Equal(this.Paths.RecentFilesFile, problem.Path);
			Assert.Equal("locked by an antivirus", problem.Detail);
			Assert.Equal("[]", File.ReadAllText(this.Paths.RecentFilesFile));
		}

		[Fact]
		public async Task A_file_that_cannot_be_written_is_reported_by_both_save_methods()
		{
			SettingsService service = this.CreateService();
			service.Load();
			service.Settings.Editor.TabSize = 7;
			service.Save();
			File.SetAttributes(this.Paths.SettingsFile, FileAttributes.ReadOnly);
			try
			{
				service.Settings.Editor.TabSize = 6;
				Assert.ThrowsAny<Exception>(() => service.Save());
				service.Settings.Editor.TabSize = 5;
				await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAsync());

				SettingsProblem problem = Assert.Single(service.Problems);
				Assert.Equal(SettingsProblemKind.SaveFailed, problem.Kind);
				Assert.Equal(this.Paths.SettingsFile, problem.Path);
				Assert.False(String.IsNullOrEmpty(problem.Detail));
			}
			finally
			{
				File.SetAttributes(this.Paths.SettingsFile, FileAttributes.Normal);
			}
		}

		[Fact]
		public void The_same_problem_is_listed_once()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.SettingsFile, "{ this is not json");
			SettingsService service = this.CreateService();

			service.Load();
			service.Load();

			Assert.Single(service.Problems, problem => problem.Kind == SettingsProblemKind.Damaged);
		}

		[Fact]
		public async Task The_async_load_reports_a_damaged_file_too()
		{
			Directory.CreateDirectory(this.Paths.SettingsDirectory);
			File.WriteAllText(this.Paths.FoldingFile, "not json either");
			SettingsService service = this.CreateService();

			await service.LoadAsync();

			Assert.Equal(this.Paths.FoldingFile, Assert.Single(service.Problems).Path);
		}
	}
}