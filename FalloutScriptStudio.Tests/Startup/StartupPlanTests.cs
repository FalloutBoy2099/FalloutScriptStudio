namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Startup;
	using Xunit;

	public sealed class StartupPlanTests
	{
		[Fact]
		public void FirstInstance_WithoutArguments_StartsTheMainEditor()
		{
			StartupPlan plan = StartupPlan.Create(Array.Empty<String>(), true);

			Assert.Equal(StartupMode.MainEditor, plan.Mode);
			Assert.True(plan.IsPrimary);
			Assert.True(plan.OwnsScratch);
			Assert.Null(plan.MessageFilePath);
		}

		[Theory]
		[InlineData("script.ssl")]
		[InlineData("C:\\mods\\notes.msg.txt")]
		[InlineData("C:\\mods\\msg")]
		public void FirstInstance_WithAnotherFile_StartsTheMainEditor(String argument)
		{
			StartupPlan plan = StartupPlan.Create(new[] { argument }, true);

			Assert.Equal(StartupMode.MainEditor, plan.Mode);
			Assert.True(plan.OwnsScratch);
		}

		[Theory]
		[InlineData("dialog.msg")]
		[InlineData("C:\\mods\\TEXT\\ENGLISH\\DIALOG\\Dialog.MSG")]
		public void FirstInstance_WithAMessageFile_RunsTheMessageEditorAlone(String argument)
		{
			StartupPlan plan = StartupPlan.Create(new[] { argument, "script.ssl" }, true);

			Assert.Equal(StartupMode.StandaloneMessageEditor, plan.Mode);
			Assert.Equal(argument, plan.MessageFilePath);
			Assert.True(plan.IsPrimary);
		}

		[Fact]
		public void StandaloneMessageEditor_DoesNotOwnTheScratchFolder()
		{
			StartupPlan plan = StartupPlan.Create(new[] { "dialog.msg" }, true);

			Assert.False(plan.OwnsScratch);
		}

		[Fact]
		public void OnlyTheFirstArgumentDecidesTheStandaloneMessageEditor()
		{
			StartupPlan plan = StartupPlan.Create(new[] { "script.ssl", "dialog.msg" }, true);

			Assert.Equal(StartupMode.MainEditor, plan.Mode);
		}

		[Fact]
		public void OtherInstance_WithArguments_ForwardsThemToTheRunningInstance()
		{
			StartupPlan plan = StartupPlan.Create(new[] { "script.ssl" }, false);

			Assert.Equal(StartupMode.ForwardToRunningInstance, plan.Mode);
			Assert.False(plan.IsPrimary);
			Assert.False(plan.OwnsScratch);
		}

		[Fact]
		public void OtherInstance_WithAMessageFile_ForwardsItToo()
		{
			StartupPlan plan = StartupPlan.Create(new[] { "dialog.msg" }, false);

			Assert.Equal(StartupMode.ForwardToRunningInstance, plan.Mode);
		}

		[Fact]
		public void OtherInstance_WithoutArguments_ShowsTheAlreadyRunningNotice()
		{
			StartupPlan plan = StartupPlan.Create(Array.Empty<String>(), false);

			Assert.Equal(StartupMode.AlreadyRunning, plan.Mode);
			Assert.False(plan.IsPrimary);
			Assert.False(plan.OwnsScratch);
		}

		[Fact]
		public void Create_RejectsNullArguments()
		{
			Assert.Throws<ArgumentNullException>(() => StartupPlan.Create(null!, true));
		}

		[Fact]
		public void Theme_IsTheStoredOneWhenTheLayoutFileExists()
		{
			Assert.Equal(AppTheme.Light, StartupTheme.Resolve(true, AppTheme.Light, true, HighlightTheme.Dark));
			Assert.Equal(AppTheme.System, StartupTheme.Resolve(true, AppTheme.System, false, HighlightTheme.Dark));
		}

		[Theory]
		[InlineData(HighlightTheme.Dark, AppTheme.Dark)]
		[InlineData(HighlightTheme.Light, AppTheme.Light)]
		public void Theme_FollowsTheLegacyChoiceAfterAnImport(HighlightTheme legacy, AppTheme expected)
		{
			Assert.Equal(expected, StartupTheme.Resolve(false, AppTheme.Dark, true, legacy));
		}

		[Fact]
		public void Theme_IsTheWindowsModeOtherwise()
		{
			Assert.Equal(AppTheme.System, StartupTheme.Resolve(false, AppTheme.Dark, false, HighlightTheme.Dark));
		}
	}
}