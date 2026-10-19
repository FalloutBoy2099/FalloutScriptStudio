namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;

	// Messages page: language, comment highlighting and message folders.
	public sealed partial class SettingsViewModel
	{
		[ObservableProperty]
		[NotifyCanExecuteChangedFor(nameof(RemoveMessagePathCommand))]
		[NotifyCanExecuteChangedFor(nameof(MoveMessagePathUpCommand))]
		[NotifyCanExecuteChangedFor(nameof(MoveMessagePathDownCommand))]
		private String? _selectedMessagePath;

		/// <summary>Gets the comment highlight colors to choose from.</summary>
		public IReadOnlyList<Choice<CommentHighlightColor>> CommentColorChoices { get; } = new List<Choice<CommentHighlightColor>>
		{
			new(CommentHighlightColor.LightYellow, Loc.Get("Str.ToolWindows.Choice.Color.LightYellow")),
			new(CommentHighlightColor.PaleGreen, Loc.Get("Str.ToolWindows.Choice.Color.PaleGreen")),
			new(CommentHighlightColor.Lavender, Loc.Get("Str.ToolWindows.Choice.Color.Lavender")),
		};

		/// <summary>Gets the additional folders searched for message files; folders at the top have priority.</summary>
		public ObservableCollection<String> MessagePaths { get; }

		/// <summary>Gets or sets a value indicating whether comment lines are highlighted in the Message Editor.</summary>
		public Boolean HighlightComments
		{
			get => this.Draft.Messages.HighlightComments;
			set
			{
				if (this.Draft.Messages.HighlightComments != value)
				{
					this.Draft.Messages.HighlightComments = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.IsCommentColorEnabled));
				}
			}
		}

		/// <summary>Gets a value indicating whether the comment color can be chosen.</summary>
		public Boolean IsCommentColorEnabled => this.Draft.Messages.HighlightComments;

		/// <summary>Gets or sets the language folder of the message files.</summary>
		public String Language
		{
			get => this.Draft.Messages.Language;
			set
			{
				if (!String.Equals(this.Draft.Messages.Language, value, StringComparison.Ordinal))
				{
					this.Draft.Messages.Language = value ?? String.Empty;
					this.OnPropertyChanged();
				}
			}
		}

		[RelayCommand]
		private void AddMessagePath()
		{
			String? folder = this._dialogService.PickFolder(Loc.Get("Str.ToolWindows.Settings.SelectMessageFolder"));
			if (String.IsNullOrWhiteSpace(folder))
			{
				return;
			}

			String? existing = this.MessagePaths.FirstOrDefault(path => String.Equals(path, folder, StringComparison.OrdinalIgnoreCase));
			if (existing is null)
			{
				this.MessagePaths.Insert(0, folder);
				existing = folder;
			}

			this.SelectedMessagePath = existing;
		}

		[RelayCommand(CanExecute = nameof(HasSelectedMessagePath))]
		private void RemoveMessagePath()
		{
			if (this.SelectedMessagePath is null)
			{
				return;
			}

			Int32 index = this.MessagePaths.IndexOf(this.SelectedMessagePath);
			this.MessagePaths.RemoveAt(index);
			this.SelectedMessagePath = this.MessagePaths.Count == 0 ? null : this.MessagePaths[Math.Min(index, this.MessagePaths.Count - 1)];
		}

		[RelayCommand(CanExecute = nameof(CanMoveMessagePathUp))]
		private void MoveMessagePathUp()
		{
			this.MoveSelectedMessagePath(-1);
		}

		[RelayCommand(CanExecute = nameof(CanMoveMessagePathDown))]
		private void MoveMessagePathDown()
		{
			this.MoveSelectedMessagePath(1);
		}

		private Boolean HasSelectedMessagePath()
		{
			return this.SelectedMessagePath is not null;
		}

		private Boolean CanMoveMessagePathUp()
		{
			return this.SelectedMessagePath is not null && this.MessagePaths.IndexOf(this.SelectedMessagePath) > 0;
		}

		private Boolean CanMoveMessagePathDown()
		{
			return this.SelectedMessagePath is not null && this.MessagePaths.IndexOf(this.SelectedMessagePath) is Int32 index && index >= 0 && index < this.MessagePaths.Count - 1;
		}

		private void MoveSelectedMessagePath(Int32 delta)
		{
			if (this.SelectedMessagePath is null)
			{
				return;
			}

			String selected = this.SelectedMessagePath;
			Int32 index = this.MessagePaths.IndexOf(selected);
			Int32 target = index + delta;
			if (index < 0 || target < 0 || target >= this.MessagePaths.Count)
			{
				return;
			}

			this.MessagePaths.Move(index, target);
			this.SelectedMessagePath = selected;
			this.MoveMessagePathUpCommand.NotifyCanExecuteChanged();
			this.MoveMessagePathDownCommand.NotifyCanExecuteChanged();
		}
	}
}