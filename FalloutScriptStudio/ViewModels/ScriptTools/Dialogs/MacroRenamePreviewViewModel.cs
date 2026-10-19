namespace FalloutScriptStudio.ViewModels.ScriptTools.Dialogs
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// View model of the global macro rename preview (legacy <c>PreviewRename</c> form): the macro definition before and after,
	/// and every occurrence of the macro name with a check box. Only the ticked occurrences are renamed.
	/// </summary>
	public sealed partial class MacroRenamePreviewViewModel : DialogueDialogViewModelBase
	{
		private readonly List<MacroRenameNodeViewModel> _matchNodes = [];
		private Int32 _selectedCount;

		/// <summary>Gets the title of the window.</summary>
		[ObservableProperty]
		public partial String Title { get; private set; } = Loc.Get("Str.ScriptTools.MacroTitle");

		/// <summary>Gets the definition of the macro with its current name.</summary>
		[ObservableProperty]
		public partial String OldDefinition { get; private set; } = String.Empty;

		/// <summary>Gets the definition of the macro with the new name.</summary>
		[ObservableProperty]
		public partial String NewDefinition { get; private set; } = String.Empty;

		/// <summary>Gets the place where the macro is defined ("file, line N").</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(DeclaredAtCaption))]
		public partial String DeclaredAt { get; private set; } = String.Empty;

		/// <summary>Gets the place where the macro is defined as a sentence ("Defined in file, line N"); empty when it is not known.</summary>
		public String DeclaredAtCaption => this.DeclaredAt.Length == 0 ? String.Empty : Loc.Format("Str.ScriptTools.MacroDefinedIn", this.DeclaredAt);

		/// <summary>Gets the caption with the number of ticked matches.</summary>
		[ObservableProperty]
		public partial String Summary { get; private set; } = String.Empty;

		/// <summary>Gets the groups of the tree (open scripts, project files).</summary>
		public IReadOnlyList<MacroRenameNodeViewModel> Sections { get; private set; } = Array.Empty<MacroRenameNodeViewModel>();

		/// <summary>Gets the number of occurrences found.</summary>
		public Int32 TotalCount => this._matchNodes.Count;

		/// <summary>Gets the number of ticked occurrences.</summary>
		public Int32 SelectedCount => this._selectedCount;

		/// <summary>Gets a value indicating whether nothing was found.</summary>
		public Boolean IsEmpty => this._matchNodes.Count == 0;

		/// <summary>Gets the occurrences the user accepted, in the order of the preview, or <see langword="null"/> when the dialog was cancelled.</summary>
		public IReadOnlyList<PreviewMatch>? AcceptedMatches { get; private set; }

		/// <summary>Builds the tree from a preview.</summary>
		/// <param name="preview">The occurrences of the macro name.</param>
		public void Initialize(MacroRenamePreview preview)
		{
			ArgumentNullException.ThrowIfNull(preview);

			foreach (MacroRenameNodeViewModel node in this._matchNodes)
			{
				node.PropertyChanged -= this.OnMatchPropertyChanged;
			}

			this._matchNodes.Clear();

			this.Title = Loc.Format("Str.ScriptTools.MacroTitleRename", preview.Macro.Token, preview.NewName);
			this.OldDefinition = DescribeDefinition(preview.Macro.DefinitionName, preview.Macro.Code);
			this.NewDefinition = DescribeDefinition(RenameDefinition(preview.Macro.DefinitionName, preview.Macro.Token, preview.NewName), preview.Macro.Code);
			this.DeclaredAt = preview.Macro.DeclaredFile.Length == 0
				? String.Empty
				: Loc.Format("Str.ScriptTools.MacroDeclaredAt", preview.Macro.DeclaredFile, preview.Macro.DeclaredLine);

			MacroRenameNodeViewModel? open = null;
			MacroRenameNodeViewModel? project = null;
			Dictionary<String, MacroRenameNodeViewModel> files = new(StringComparer.OrdinalIgnoreCase);
			List<MacroRenameNodeViewModel> sections = [];
			for (Int32 index = 0; index < preview.Matches.Count; index++)
			{
				PreviewMatch match = preview.Matches[index];
				if (!files.TryGetValue(match.FilePath, out MacroRenameNodeViewModel? file))
				{
					MacroRenameNodeViewModel section;
					if (match.IsOpenDocument)
					{
						section = open ??= MacroRenameNodeViewModel.CreateSection(Loc.Get("Str.ScriptTools.MacroSectionOpen"));
					}
					else
					{
						section = project ??= MacroRenameNodeViewModel.CreateSection(Loc.Get("Str.ScriptTools.MacroSectionProject"));
					}

					if (!sections.Contains(section))
					{
						sections.Add(section);
					}

					file = MacroRenameNodeViewModel.AddFile(section, match.FilePath);
					files.Add(match.FilePath, file);
				}

				MacroRenameNodeViewModel node = MacroRenameNodeViewModel.AddMatch(file, match, index);
				node.PropertyChanged += this.OnMatchPropertyChanged;
				this._matchNodes.Add(node);
			}

			this.Sections = sections.OrderBy(section => ReferenceEquals(section, project) ? 1 : 0).ToList();
			this._selectedCount = this._matchNodes.Count;
			this.OnPropertyChanged(nameof(this.Sections));
			this.OnPropertyChanged(nameof(this.TotalCount));
			this.OnPropertyChanged(nameof(this.IsEmpty));
			this.UpdateSelection();
		}

		/// <summary>Builds the definition of the macro with another name.</summary>
		/// <param name="definitionName">The name with its parameter list, for example <c>MAX(a, b)</c>.</param>
		/// <param name="token">The current name.</param>
		/// <param name="newName">The new name.</param>
		/// <returns>The definition with the new name; the parameters are left alone.</returns>
		public static String RenameDefinition(String definitionName, String token, String newName)
		{
			ArgumentNullException.ThrowIfNull(definitionName);
			ArgumentNullException.ThrowIfNull(token);
			ArgumentNullException.ThrowIfNull(newName);

			return token.Length > 0 && definitionName.StartsWith(token, StringComparison.Ordinal)
				? String.Concat(newName, definitionName.AsSpan(token.Length))
				: definitionName.Replace(token, newName, StringComparison.Ordinal);
		}

		[RelayCommand]
		private void SelectAll()
		{
			this.SetAll(true);
		}

		[RelayCommand]
		private void DeselectAll()
		{
			this.SetAll(false);
		}

		[RelayCommand(CanExecute = nameof(CanApply))]
		private void Apply()
		{
			this.AcceptedMatches = this._matchNodes
				.Where(node => node.IsChecked == true && node.Match is not null)
				.OrderBy(node => node.Index)
				.Select(node => node.Match!)
				.ToList();
			this.RequestClose(true);
		}

		private Boolean CanApply()
		{
			return this._selectedCount > 0;
		}

		[RelayCommand]
		private void Cancel()
		{
			this.AcceptedMatches = null;
			this.RequestClose(false);
		}

		private void SetAll(Boolean value)
		{
			foreach (MacroRenameNodeViewModel section in this.Sections)
			{
				section.IsChecked = value;
			}
		}

		private void OnMatchPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(MacroRenameNodeViewModel.IsChecked) || sender is not MacroRenameNodeViewModel node)
			{
				return;
			}

			this._selectedCount += node.IsChecked == true ? 1 : -1;
			this.UpdateSelection();
		}

		private void UpdateSelection()
		{
			Int32 files = this.Sections.Sum(section => section.Children.Count);
			String key = this._matchNodes.Count == 0
				? "Str.ScriptTools.MacroSummaryNone"
				: this._matchNodes.Count == 1 ? "Str.ScriptTools.MacroSummaryOne"
				: files == 1 ? "Str.ScriptTools.MacroSummaryOneFile"
				: "Str.ScriptTools.MacroSummaryMany";
			this.Summary = Loc.Format(key, this._selectedCount, this._matchNodes.Count, files);
			this.OnPropertyChanged(nameof(this.SelectedCount));
			this.ApplyCommand.NotifyCanExecuteChanged();
		}

		private static String DescribeDefinition(String definitionName, String code)
		{
			String firstLine = code.Split('\n')[0].TrimEnd('\r', '\\', ' ', '\t').Trim();
			Boolean more = code.Contains('\n', StringComparison.Ordinal);
			String body = firstLine.Length == 0 ? String.Empty : " " + firstLine;
			return "#define " + definitionName + body + (more ? " ..." : String.Empty);
		}
	}
}