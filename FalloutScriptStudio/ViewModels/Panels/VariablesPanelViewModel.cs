namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Workspace;

	/// <summary>The "Variables" tab of the right panel: global and script variables.</summary>
	public sealed partial class VariablesPanelViewModel : ObservableObject
	{
		private readonly IWorkspace _workspace;
		private readonly ActiveEditorTracker _tracker;
		private readonly ConditionalWeakTable<DocumentViewModel, Dictionary<String, Boolean>> _expandState = [];
		private DocumentViewModel? _document;

		/// <summary>Initializes the panel.</summary>
		public VariablesPanelViewModel(IWorkspace workspace, ActiveEditorTracker tracker)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(tracker);
			this._workspace = workspace;
			this._tracker = tracker;
			this._workspace.ActiveDocumentChanged += (_, _) => this.Rebuild();
			this._workspace.DocumentParsed += (_, document) =>
			{
				if (ReferenceEquals(document, this._workspace.ActiveDocument))
				{
					this.Rebuild();
				}
			};
			_ = LanguageChangedSubscription.Subscribe(this, static panel => panel.Rebuild());
			this.Rebuild();
		}

		/// <summary>Gets the groups of the tree.</summary>
		public ObservableCollection<TreeGroupNodeViewModel> Groups { get; } = [];

		/// <summary>True when the active document has variable data to show.</summary>
		[ObservableProperty]
		public partial Boolean HasData { get; set; }

		/// <summary>The text the tree is filtered with.</summary>
		[ObservableProperty]
		public partial String FilterText { get; set; } = String.Empty;

		partial void OnFilterTextChanged(String value)
		{
			this.ApplyFilter();
		}

		private void ApplyFilter()
		{
			_ = TreeFilter.Apply(this.Groups, this.FilterText);
		}

		/// <summary>
		/// Brings the tree up to date with the parse data of the active document: the nodes of the document that is already shown are
		/// updated in place, the tree of another document is built from scratch.
		/// </summary>
		public void Rebuild()
		{
			DocumentViewModel? previous = this._document;
			if (this._document is not null && this.Groups.Count > 0)
			{
				Dictionary<String, Boolean> old = this._expandState.GetOrCreateValue(this._document);
				foreach (TreeGroupNodeViewModel group in this.Groups)
				{
					old[group.Key] = group.IsExpanded;
				}
			}

			this._document = this._workspace.ActiveDocument;
			ProgramInfo? info = this._document?.ProgramInfo;
			if (this._document is null || info is null)
			{
				this.Groups.Clear();
				this.HasData = false;
				return;
			}

			Dictionary<String, Boolean> state = this._expandState.GetOrCreateValue(this._document);
			IReadOnlyList<TreeGroupNodeViewModel> fresh = SymbolTreeBuilder.BuildVariableGroups(info, this._document.FileName);
			if (!ReferenceEquals(previous, this._document) || !TreeSync.TryMergeGroups(this.Groups, fresh, _ => { }))
			{
				this.Groups.Clear();
				foreach (TreeGroupNodeViewModel group in fresh)
				{
					group.IsExpanded = state.TryGetValue(group.Key, out Boolean expanded) ? expanded : true;
					this.Groups.Add(group);
				}
			}

			this.HasData = this.Groups.Count > 0;
			this.ApplyFilter();
		}

		/// <summary>Goes to the declaration of a variable.</summary>
		[RelayCommand]
		public async Task NavigateAsync(TreeNodeViewModel? node)
		{
			if (node is VariableNodeViewModel { Variable.DeclaredLine: > 0 } variable)
			{
				await this._workspace.GoToAsync(variable.Variable.DeclaredFile, variable.Variable.DeclaredLine);
			}
		}

		/// <summary>Inserts the name of a variable into the script at the caret.</summary>
		[RelayCommand]
		public void InsertName(TreeNodeViewModel? node)
		{
			if (node is VariableNodeViewModel variable)
			{
				this._tracker.Editor?.InsertText(variable.Variable.Name);
			}
		}

		/// <summary>Lists the references of a variable in the Errors tab.</summary>
		[RelayCommand]
		public async Task FindReferencesAsync(TreeNodeViewModel? node)
		{
			if (node is VariableNodeViewModel variable)
			{
				await this._workspace.ShowReferencesAsync(variable.Variable.Name, variable.Variable.References);
			}
		}

		/// <summary>Lists the variables of the script that are declared but never used in the list of problems.</summary>
		[RelayCommand]
		public async Task FindUnusedAsync()
		{
			if (this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.FindUnusedVariablesAsync(document);
			}
		}

		/// <summary>Renames a variable everywhere it is used.</summary>
		[RelayCommand]
		public async Task RenameAsync(TreeNodeViewModel? node)
		{
			if (node is VariableNodeViewModel variable && this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.RenameSymbolAsync(document, variable.Variable);
			}
		}
	}
}