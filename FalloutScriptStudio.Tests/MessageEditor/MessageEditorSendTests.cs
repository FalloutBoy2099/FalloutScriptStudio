namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	public sealed class MessageEditorSendTests
	{
		private static readonly String[] _sample =
		{
			"# Intro",
			"{100}{}{Hello there}",
			"{101}{}{Second line}",
		};

		[Fact]
		public void SendLineGivesTheCurrentMessageToTheTarget()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, false);
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(2);

				Assert.True(harness.Editor.SendLineCommand.CanExecute(null));
				harness.Editor.SendLineCommand.Execute(null);

				Assert.Equal(new[] { (101, "Second line") }, target.Received);
				Assert.Contains("101", harness.Editor.StatusMessage, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void SendLineIsUnavailableWithoutATarget()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(2);

				Assert.False(harness.Editor.SendLineCommand.CanExecute(null));
				Assert.False(harness.Editor.HasTarget);
			});
		}

		[Fact]
		public void SendLineIsUnavailableOnCommentsAndWhenTheTargetRefuses()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, false);
				await harness.OpenAsync("a.msg", _sample);

				harness.Select(0);
				Assert.False(harness.Editor.SendLineCommand.CanExecute(null));

				harness.Select(1);
				Assert.True(harness.Editor.SendLineCommand.CanExecute(null));

				target.CanAcceptMessageLine = false;
				harness.Editor.RefreshTargetState();
				Assert.False(harness.Editor.SendLineCommand.CanExecute(null));
				harness.Editor.SendLineCommand.Execute(null);
				Assert.Empty(target.Received);
			});
		}

		[Fact]
		public void TheWindowClosesAfterSendingWhenRequested()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, true);
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(1);
				Boolean closeRequested = false;
				harness.Editor.CloseRequested += (_, _) => closeRequested = true;

				harness.Editor.SendLineCommand.Execute(null);

				Assert.True(closeRequested);
				Assert.Single(target.Received);
			});
		}

		[Fact]
		public void SendRowSelectsAndSendsTheClickedRow()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, false);
				await harness.OpenAsync("a.msg", _sample);
				harness.Select(1);

				harness.Editor.SendRow(harness.Rows[2]);

				Assert.Equal(new[] { (101, "Second line") }, target.Received);
				Assert.Same(harness.Rows[2], harness.FocusedRows[0]);
			});
		}

		[Fact]
		public void SendLineUsesTheEditedText()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, false);
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[1].Text = "Changed";
				harness.Select(1);

				harness.Editor.SendLineCommand.Execute(null);

				Assert.Equal(new[] { (100, "Changed") }, target.Received);
			});
		}

		[Fact]
		public void TheScriptCaptionNamesTheOwner()
		{
			StaTestRunner.Run(() =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				Assert.Equal(String.Empty, harness.Editor.ScriptCaption);

				harness.Editor.AttachTarget(new FakeLineTarget { ScriptPath = @"D:\scripts\hakunin.ssl" }, false);

				Assert.Equal("Script: hakunin.ssl", harness.Editor.ScriptCaption);
			});
		}
	}
}