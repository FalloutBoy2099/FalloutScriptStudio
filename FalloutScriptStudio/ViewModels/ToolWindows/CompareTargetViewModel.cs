namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.IO;
	using CommunityToolkit.Mvvm.ComponentModel;
	using Core.Messages;
	using Localization;

	/// <summary>One target message file of the comparison (a language): its path, what was read from it and the comparison with the reference.</summary>
	public sealed class CompareTargetViewModel : ObservableObject
	{
		private LoadedMessageFile? _file;
		private MessageComparison _comparison = MessageComparison.Empty;
		private String? _error;

		/// <summary>Initializes a new instance of the <see cref="CompareTargetViewModel"/> class.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <param name="language">The language folder of the file, or <see langword="null"/>.</param>
		public CompareTargetViewModel(String path, String? language)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			this.Path = path;
			this.Language = language;
		}

		/// <summary>Gets the full path of the file.</summary>
		public String Path { get; }

		/// <summary>Gets the language folder of the file, or <see langword="null"/>.</summary>
		public String? Language { get; }

		/// <summary>Gets what was read from the file, or <see langword="null"/> when it could not be read.</summary>
		public LoadedMessageFile? File => this._file;

		/// <summary>Gets the comparison with the reference.</summary>
		public MessageComparison Comparison => this._comparison;

		/// <summary>Gets why the file could not be read, or <see langword="null"/>.</summary>
		public String? Error => this._error;

		/// <summary>Gets the name shown in the list of targets: the language, the file name and the number of lines to look at.</summary>
		public String DisplayName
		{
			get
			{
				String name = (this.Language is null ? String.Empty : this.Language + " - ") + System.IO.Path.GetFileName(this.Path);
				if (this._error is not null)
				{
					return Loc.Format("Str.ToolWindows.Compare.TargetUnreadable", name);
				}

				return this._file is null ? name : Loc.Format("Str.ToolWindows.Compare.TargetLabel", name, this._comparison.Rows.Count - this._comparison.Count(MessageComparisonStatus.Ok));
			}
		}

		/// <summary>Stores a new comparison.</summary>
		/// <param name="file">The file as it was read.</param>
		/// <param name="comparison">The comparison.</param>
		public void SetResult(LoadedMessageFile file, MessageComparison comparison)
		{
			this._file = file;
			this._comparison = comparison;
			this._error = null;
			this.OnPropertyChanged(nameof(this.File));
			this.OnPropertyChanged(nameof(this.Comparison));
			this.OnPropertyChanged(nameof(this.Error));
			this.OnPropertyChanged(nameof(this.DisplayName));
		}

		/// <summary>Records that the file could not be read.</summary>
		/// <param name="error">The reason.</param>
		public void SetError(String error)
		{
			this._file = null;
			this._comparison = MessageComparison.Empty;
			this._error = error;
			this.OnPropertyChanged(nameof(this.File));
			this.OnPropertyChanged(nameof(this.Comparison));
			this.OnPropertyChanged(nameof(this.Error));
			this.OnPropertyChanged(nameof(this.DisplayName));
		}

		/// <summary>Raises the change of the texts after a language change.</summary>
		public void RefreshTexts()
		{
			this.OnPropertyChanged(nameof(this.DisplayName));
		}
	}
}