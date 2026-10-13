namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Workspace;

	/// <summary>The "Procedures" tab of the right panel: global and local procedures with their variables, and the procedure operations.</summary>
	public sealed partial class ProceduresPanelViewModel : ObservableObject
	{
		private readonly IWorkspaceManager _workspace;
		private readonly ActiveEditorTracker _tracker;
		private readonly IDialogService _dialogs;
		private readonly INodeCodeEditorService _nodeEditors;
		private readonly ConditionalWeakTable<DocumentViewModel, Dictionary<String, Boolean>> _expandState = new ConditionalWeakTable<DocumentViewModel, Dictionary<String, Boolean>>();
		private DocumentViewModel? _document;

		/// <summary>Initializes the panel.</summary>
		public ProceduresPanelViewModel(IWorkspaceManager workspace, ActiveEditorTracker tracker, IDialogService dialogs, ISettingsService settings, INodeCodeEditorService nodeEditors)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(tracker);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(nodeEditors);
			this._workspace = workspace;
			this._tracker = tracker;
			this._dialogs = dialogs;
			this._nodeEditors = nodeEditors;
			this.Handlers = LoadHandlers(System.IO.Path.Combine(settings.Paths.DescriptionsDirectory, "prochandlers.ini"));

			this._workspace.ActiveDocumentChanged += (_, _) => this.Rebuild();
			this._workspace.DocumentParsed += (_, document) =>
			{
				if (ReferenceEquals(document, this._workspace.ActiveDocument))
				{
					this.Rebuild();
				}
			};
			this._tracker.CurrentProcedureChanged += (_, _) => this.HighlightCurrent();
			this._tracker.CaretMoved += (_, _) => this.HighlightCurrent();
			_ = LanguageChangedSubscription.Subscribe(this, static panel => panel.Rebuild());
			this.Rebuild();
		}

		/// <summary>Gets the groups of the tree.</summary>
		public ObservableCollection<TreeGroupNodeViewModel> Groups { get; } = new ObservableCollection<TreeGroupNodeViewModel>();

		/// <summary>Gets the menu of standard handlers that can be created.</summary>
		public IReadOnlyList<HandlerMenuItemViewModel> Handlers { get; }

		/// <summary>True when the argument lists are shown next to the procedure names.</summary>
		[ObservableProperty]
		public partial Boolean ShowArguments { get; set; }

		/// <summary>
		/// True when going to a procedure collapses all the other procedures, so only the target stays open (legacy "only procedure"
		/// button). Not persisted, like the legacy toggle button.
		/// </summary>
		[ObservableProperty]
		public partial Boolean OnlyCurrentProcedure { get; set; }

		/// <summary>True when the active document has parse data to show.</summary>
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

		/// <summary>The selected node.</summary>
		[ObservableProperty]
		public partial TreeNodeViewModel? SelectedNode { get; set; }

		/// <summary>
		/// Brings the tree up to date with the parse data of the active document. The nodes of the document that is already shown are
		/// updated in place (a tree that is built again makes WPF create all its items again after every parse); the tree of another
		/// document is built from scratch.
		/// </summary>
		public void Rebuild()
		{
			DocumentViewModel? previous = this._document;
			this.StoreExpandState();
			this._document = this._workspace.ActiveDocument;
			String? selectedKey = this.SelectedNode?.Key;

			ProgramInfo? info = this._document?.ProgramInfo;
			this.HasData = info is not null && this._document is { IsParsable: true };
			if (this._document is null || info is null || !this.HasData)
			{
				this.Groups.Clear();
				this.SelectedNode = null;
				return;
			}

			Dictionary<String, Boolean> state = this._expandState.GetOrCreateValue(this._document);
			IReadOnlyList<TreeGroupNodeViewModel> fresh = SymbolTreeBuilder.BuildProcedureGroups(info, this._document.FileName, this.ShowArguments);
			if (!ReferenceEquals(previous, this._document) || !TreeSync.TryMergeGroups(this.Groups, fresh, node => ApplyExpandState(node, state, false)))
			{
				this.Groups.Clear();
				foreach (TreeGroupNodeViewModel group in fresh)
				{
					ApplyExpandState(group, state, true);
					this.Groups.Add(group);
				}
			}

			if (selectedKey is not null)
			{
				this.SelectedNode = this.FindNode(selectedKey);
				if (this.SelectedNode is not null)
				{
					this.SelectedNode.IsSelected = true;
				}
			}

			this.ApplyFilter();
			this.HighlightCurrent();
		}

		/// <summary>Marks the procedure that contains the caret.</summary>
		public void HighlightCurrent()
		{
			String? current = this._tracker.Editor?.CurrentProcedure?.NormalizedName;
			if (current is null && this._tracker.Editor is { } editor && this._document?.ProgramInfo is { } info)
			{
				current = info.GetProcedureFromPosition(editor.CaretLine)?.NormalizedName;
			}

			foreach (ProcedureNodeViewModel node in this.Groups.SelectMany(g => g.Children).OfType<ProcedureNodeViewModel>())
			{
				node.IsCurrent = current is not null && String.Equals(node.Procedure.NormalizedName, current, StringComparison.Ordinal);
			}
		}

		/// <summary>Goes to the body of a procedure (or the declaration of a variable).</summary>
		[RelayCommand]
		public async Task NavigateAsync(TreeNodeViewModel? node)
		{
			switch (node)
			{
				case ProcedureNodeViewModel procedureNode:
					ScriptProcedure procedure = procedureNode.Procedure;
					if (procedure.StartLine > 0 && !String.IsNullOrEmpty(procedure.StartFile))
					{
						await this._workspace.GoToAsync(procedure.StartFile, procedure.StartLine);
					}
					else if (procedure.DeclaredLine > 0)
					{
						await this._workspace.GoToAsync(procedure.DeclaredFile, procedure.DeclaredLine);
					}

					if (this.OnlyCurrentProcedure)
					{
						this._tracker.Editor?.Execute(EditorCommand.ShowOnlyCurrentProcedure);
					}

					break;
				case VariableNodeViewModel variableNode when variableNode.Variable.DeclaredLine > 0:
					await this._workspace.GoToAsync(variableNode.Variable.DeclaredFile, variableNode.Variable.DeclaredLine);
					break;
			}
		}

		/// <summary>Inserts the name of a node into the script at the caret.</summary>
		[RelayCommand]
		public void InsertName(TreeNodeViewModel? node)
		{
			if (node is ProcedureNodeViewModel or VariableNodeViewModel)
			{
				this._tracker.Editor?.InsertText(node is ProcedureNodeViewModel p ? p.Procedure.Name : ((VariableNodeViewModel)node).Variable.Name);
			}
		}

		/// <summary>
		/// Asks for a name and creates a procedure. Like the legacy editor, the name starts as the selected procedure of the tree (or else the
		/// selected text) with its trailing number incremented, and the selected procedure of the tree is the one that "after the procedure"
		/// and "copy the body" refer to.
		/// </summary>
		[RelayCommand]
		public async Task CreateProcedureAsync()
		{
			if (this._workspace.ActiveDocument is not { } document)
			{
				return;
			}

			ProcedureNodeViewModel? anchor = this.GetSelectedLocalProcedure();
			String? word = this.SelectedNode is ProcedureNodeViewModel selected ? selected.Procedure.Name : this._tracker.Editor?.SelectedText;
			await this._workspace.CreateProcedureAsync(document, String.IsNullOrWhiteSpace(word) ? null : word.Trim(), anchor?.Procedure.Name);
		}

		/// <summary>Creates one of the standard handlers.</summary>
		[RelayCommand]
		public async Task CreateHandlerAsync(HandlerMenuItemViewModel? handler)
		{
			if (handler is { IsGroup: false, IsSeparator: false } && this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.CreateHandlerAsync(document, handler.Name, this.GetSelectedLocalProcedure()?.Procedure.Name);
			}
		}

		/// <summary>Opens the node code editor of a dialogue node procedure (legacy "Edit Node code" of the procedures tree).</summary>
		[RelayCommand]
		public async Task EditNodeCodeAsync(ProcedureNodeViewModel? node)
		{
			if (node is not { CanEditNodeCode: true } || this._workspace.ActiveDocument is not { } document)
			{
				return;
			}

			if (await this._workspace.AssociateMessageFileAsync(document, false, true) is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MissingMessageFile.Title"), Loc.Get("Str.Flowchart.Msg.MissingOwnMessageFile"));
				return;
			}

			_ = this._nodeEditors.Open(document, node.Procedure.Name, false);
		}

		/// <summary>Renames a procedure everywhere it is used.</summary>
		[RelayCommand]
		public async Task RenameAsync(ProcedureNodeViewModel? node)
		{
			if (node is not null && this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.RenameSymbolAsync(document, node.Procedure);
			}
		}

		/// <summary>Deletes a procedure after confirmation.</summary>
		[RelayCommand]
		public async Task DeleteAsync(ProcedureNodeViewModel? node)
		{
			if (node is null || this._workspace.ActiveDocument is not { } document)
			{
				return;
			}

			if (await this._dialogs.ConfirmAsync(Loc.Get("Str.Panels.MsgDeleteProcedureTitle"), Loc.Format("Str.Panels.MsgConfirmDeleteProcedure", node.Procedure.Name)))
			{
				await this._workspace.DeleteProcedureAsync(document, node.Procedure.Name);
			}
		}

		/// <summary>Deletes a procedure that nothing uses, after showing what is removed; a procedure that is still used is refused and its uses listed.</summary>
		[RelayCommand]
		public async Task DeleteUnusedAsync(ProcedureNodeViewModel? node)
		{
			if (node is not null && this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.DeleteUnusedProcedureAsync(document, node.Procedure.Name);
			}
		}

		/// <summary>Moves a procedure one position up.</summary>
		[RelayCommand]
		public Task MoveUpAsync(ProcedureNodeViewModel? node)
		{
			return this.MoveRelativeAsync(node, -1);
		}

		/// <summary>Moves a procedure one position down.</summary>
		[RelayCommand]
		public Task MoveDownAsync(ProcedureNodeViewModel? node)
		{
			return this.MoveRelativeAsync(node, 1);
		}

		/// <summary>Moves a procedure to the place of another one (drag and drop).</summary>
		public async Task MoveToAsync(ProcedureNodeViewModel source, ProcedureNodeViewModel target)
		{
			if (!source.IsLocal || !target.IsLocal || ReferenceEquals(source, target) || this._workspace.ActiveDocument is not { } document)
			{
				return;
			}

			TreeGroupNodeViewModel? group = this.Groups.FirstOrDefault(g => g.IsLocal);
			if (group is null)
			{
				return;
			}

			Int32 sourceIndex = group.Children.IndexOf(source);
			Int32 targetIndex = group.Children.IndexOf(target);
			if (sourceIndex < 0 || targetIndex < 0)
			{
				return;
			}

			String? before = targetIndex < sourceIndex ? target.Procedure.Name : GetProcedureNameAt(group, targetIndex + 1);
			await this._workspace.MoveProcedureAsync(document, source.Procedure.Name, before);
		}

		/// <summary>Lists the references of a procedure in the Errors tab.</summary>
		[RelayCommand]
		public async Task FindReferencesAsync(ProcedureNodeViewModel? node)
		{
			if (node is not null)
			{
				await this._workspace.ShowReferencesAsync(node.Procedure.Name, node.Procedure.References);
			}
		}

		/// <summary>Goes to the forward declaration of a procedure.</summary>
		[RelayCommand]
		public async Task GoToDeclarationAsync(ProcedureNodeViewModel? node)
		{
			if (node is { Procedure.DeclaredLine: > 0 })
			{
				await this._workspace.GoToAsync(node.Procedure.DeclaredFile, node.Procedure.DeclaredLine);
			}
		}

		/// <summary>Forces a new parse of the active script.</summary>
		[RelayCommand]
		public async Task RefreshAsync()
		{
			if (this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.RequestParseAsync(document, true);
			}
		}

		/// <summary>Moves the caret to the begin of the script's first procedure.</summary>
		[RelayCommand]
		public void GoToBegin()
		{
			this._tracker.Editor?.Execute(EditorCommand.GoToProcedureBegin);
		}

		/// <summary>Collapses or expands all procedures of the editor.</summary>
		[RelayCommand]
		public void ToggleOutline()
		{
			this._tracker.Editor?.Execute(EditorCommand.ToggleOutline);
		}

		partial void OnShowArgumentsChanged(Boolean value)
		{
			this.Rebuild();
		}

		private static void ApplyExpandState(TreeNodeViewModel node, Dictionary<String, Boolean> state, Boolean isRoot)
		{
			node.IsExpanded = state.TryGetValue(node.Key, out Boolean expanded) ? expanded : isRoot;
			foreach (TreeNodeViewModel child in node.Children)
			{
				ApplyExpandState(child, state, false);
			}
		}

		private static void CollectExpandState(TreeNodeViewModel node, Dictionary<String, Boolean> state)
		{
			state[node.Key] = node.IsExpanded;
			foreach (TreeNodeViewModel child in node.Children)
			{
				CollectExpandState(child, state);
			}
		}

		private void StoreExpandState()
		{
			if (this._document is null || this.Groups.Count == 0)
			{
				return;
			}

			Dictionary<String, Boolean> state = this._expandState.GetOrCreateValue(this._document);
			foreach (TreeGroupNodeViewModel group in this.Groups)
			{
				CollectExpandState(group, state);
			}
		}

		private ProcedureNodeViewModel? GetSelectedLocalProcedure()
		{
			return this.SelectedNode is ProcedureNodeViewModel { IsLocal: true } node ? node : null;
		}

		private TreeNodeViewModel? FindNode(String key)
		{
			Stack<TreeNodeViewModel> pending = new Stack<TreeNodeViewModel>(this.Groups);
			while (pending.Count > 0)
			{
				TreeNodeViewModel node = pending.Pop();
				if (String.Equals(node.Key, key, StringComparison.Ordinal))
				{
					return node;
				}

				foreach (TreeNodeViewModel child in node.Children)
				{
					pending.Push(child);
				}
			}

			return null;
		}

		private static HandlerMenuItemViewModel[] LoadHandlers(String path)
		{
			return System.IO.File.Exists(path)
				? ProcedureHandlerCatalog.Parse(System.IO.File.ReadAllLines(path)).Entries.Select(entry => new HandlerMenuItemViewModel(entry)).ToArray()
				: Array.Empty<HandlerMenuItemViewModel>();
		}

		private static String? GetProcedureNameAt(TreeGroupNodeViewModel group, Int32 index)
		{
			return index >= 0 && index < group.Children.Count && group.Children[index] is ProcedureNodeViewModel node ? node.Procedure.Name : null;
		}

		private async Task MoveRelativeAsync(ProcedureNodeViewModel? node, Int32 direction)
		{
			if (node is null || !node.IsLocal || this._workspace.ActiveDocument is not { } document)
			{
				return;
			}

			TreeGroupNodeViewModel? group = this.Groups.FirstOrDefault(g => g.IsLocal);
			if (group is null)
			{
				return;
			}

			Int32 index = group.Children.IndexOf(node);
			Int32 neighbourIndex = index + direction;
			if (index < 0 || neighbourIndex < 0 || neighbourIndex >= group.Children.Count)
			{
				return;
			}

			String? before = direction < 0 ? GetProcedureNameAt(group, neighbourIndex) : GetProcedureNameAt(group, neighbourIndex + 1);
			await this._workspace.MoveProcedureAsync(document, node.Procedure.Name, before);
		}
	}
}
