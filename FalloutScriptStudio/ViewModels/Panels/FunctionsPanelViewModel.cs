namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Dialogs;
	using FalloutScriptStudio.Workspace;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>The functions tree (right tab and optional left panel): built-in and user functions that insert code.</summary>
	public sealed partial class FunctionsPanelViewModel : ObservableObject
	{
		private readonly IFunctionCatalogService _catalog;
		private readonly ActiveEditorTracker _tracker;
		private readonly IDialogService _dialogs;
		private readonly IDialogueDialogsService _editorDialog;
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private String? _keyToSelect;

		/// <summary>Initializes the panel and shows the catalog that is loaded so far.</summary>
		public FunctionsPanelViewModel(IFunctionCatalogService catalog, ActiveEditorTracker tracker, IDialogService dialogs, IDialogueDialogsService editorDialog)
		{
			ArgumentNullException.ThrowIfNull(catalog);
			ArgumentNullException.ThrowIfNull(tracker);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(editorDialog);
			this._catalog = catalog;
			this._tracker = tracker;
			this._dialogs = dialogs;
			this._editorDialog = editorDialog;
			this._catalog.CatalogChanged += this.OnCatalogChanged;
			this.Reload();
		}

		/// <summary>Gets the top level nodes.</summary>
		public ObservableCollection<FunctionNodeViewModel> Roots { get; } = [];

		/// <summary>Gets the top level nodes that pass the filter, the ones the tree shows.</summary>
		public ObservableCollection<FunctionNodeViewModel> VisibleRoots { get; } = [];

		/// <summary>The selected node.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanEditFunction), nameof(CanAddFunction), nameof(CanAddGroup), nameof(CanRenameGroup), nameof(CanDeleteNode))]
		public partial FunctionNodeViewModel? SelectedNode { get; set; }

		/// <summary>True when the selected node is a function.</summary>
		public Boolean CanEditFunction => this.SelectedNode is { } node && this._catalog.CanEditFunction(node.Node);

		/// <summary>True when a function can be added at the selected node.</summary>
		public Boolean CanAddFunction => this.SelectedNode is { } node && this._catalog.CanAddFunction(node.Node);

		/// <summary>True when a sub category can be added to the selected node.</summary>
		public Boolean CanAddGroup => this.SelectedNode is { } node && this._catalog.CanAddCategory(node.Node);

		/// <summary>True when the selected category can be renamed.</summary>
		public Boolean CanRenameGroup => this.SelectedNode is { } node && this._catalog.CanRenameCategory(node.Node);

		/// <summary>True when the selected node can be deleted.</summary>
		public Boolean CanDeleteNode => this.SelectedNode is { } node && this._catalog.CanDelete(node.Node);

		/// <summary>The text the tree is filtered with.</summary>
		[ObservableProperty]
		public partial String FilterText { get; set; } = String.Empty;

		partial void OnFilterTextChanged(String value)
		{
			this.ApplyFilter();
		}

		private void ApplyFilter()
		{
			_ = TreeFilter.Apply(this.Roots, this.FilterText);
			FunctionNodeViewModel.SyncVisible(this.VisibleRoots, this.Roots);
		}

		/// <summary>Rebuilds the tree from the catalog, keeping the expanded categories and the selection.</summary>
		public void Reload()
		{
			HashSet<String> expanded = new(StringComparer.Ordinal);
			CollectExpanded(this.Roots, expanded);
			String? selectedKey = this._keyToSelect ?? this.SelectedNode?.Key;
			this._keyToSelect = null;

			this.Roots.Clear();
			this.VisibleRoots.Clear();
			this.SelectedNode = null;
			Boolean firstLoad = expanded.Count == 0;
			foreach (FunctionNode node in this._catalog.Catalog.Roots)
			{
				this.Roots.Add(new FunctionNodeViewModel(node, null));
			}

			ApplyState(this.Roots, expanded, selectedKey, firstLoad, this);
			this.ApplyFilter();
		}

		/// <summary>Inserts the code of a function into the active editor at the caret.</summary>
		[RelayCommand]
		public void Insert(FunctionNodeViewModel? node)
		{
			IEditorSurface? editor = this._tracker.Editor;
			DocumentViewModel? document = this._tracker.Document;
			if (node is not { IsFunction: true } || editor is null || document is null)
			{
				return;
			}

			TextDocument text = document.TextDocument;
			Int32 offset = editor.CaretOffset;
			Char before = offset > 0 && offset <= text.TextLength ? text.GetCharAt(offset - 1) : '\0';
			Char after = offset < text.TextLength ? text.GetCharAt(offset) : '\0';
			Boolean hasSelection = editor.SelectionLength > 0;
			FunctionInsertion insertion = FunctionInsertion.Create(node.Node.Code, editor.CaretColumn - 1, hasSelection, before, after);
			Int32 start = hasSelection ? editor.SelectionStart : offset;
			if (hasSelection)
			{
				editor.ReplaceSelection(insertion.Text);
			}
			else
			{
				editor.InsertText(insertion.Text);
			}

			if (insertion.FirstPlaceholder is { } placeholder)
			{
				editor.Select(start + placeholder.Offset, placeholder.Length);
			}

			editor.FocusEditor();
		}

		/// <summary>Adds a user function after the selected function or as the first function of the selected category.</summary>
		[RelayCommand]
		public async Task AddFunctionAsync()
		{
			FunctionNodeViewModel? anchor = this.SelectedNode;
			if (anchor is null || !this._catalog.CanAddFunction(anchor.Node))
			{
				return;
			}

			FunctionEditResult? answer = await this._editorDialog.ShowFunctionEditorAsync(null);
			if (answer is null)
			{
				return;
			}

			FunctionOperationResult result = await this._catalog.AddFunctionAsync(anchor.Node, answer.Definition, answer.AddToHighlight);
			if (!await this.ReportAsync(result))
			{
				return;
			}

			this._keyToSelect = (anchor.IsFunction ? anchor.Parent?.Key : anchor.Key) is { } parentKey ? parentKey + "/" + answer.Definition.Name : answer.Definition.Name;
			this.Reload();
		}

		/// <summary>Edits the selected function.</summary>
		[RelayCommand]
		public async Task EditFunctionAsync()
		{
			FunctionNodeViewModel? node = this.SelectedNode;
			if (node is null || !this._catalog.CanEditFunction(node.Node))
			{
				return;
			}

			FunctionEditResult? answer = await this._editorDialog.ShowFunctionEditorAsync(FunctionDefinition.FromNode(node.Node));
			if (answer is null)
			{
				return;
			}

			if (!node.IsUser
				&& !await this._dialogs.ConfirmAsync(Loc.Get("Str.Panels.FunctionEditTitle"), Loc.Get("Str.Panels.MsgNotUserFunction")))
			{
				return;
			}

			FunctionOperationResult result = await this._catalog.EditFunctionAsync(node.Node, answer.Definition);
			if (await this.ReportAsync(result))
			{
				this._keyToSelect = (node.Parent is null ? String.Empty : node.Parent.Key + "/") + answer.Definition.Name;
				this.Reload();
			}
		}

		/// <summary>Adds a sub category to the selected category.</summary>
		[RelayCommand]
		public async Task AddGroupAsync()
		{
			FunctionNodeViewModel? parent = this.SelectedNode;
			if (parent is null || !this._catalog.CanAddCategory(parent.Node))
			{
				return;
			}

			String? name = await this._dialogs.PromptAsync(Loc.Get("Str.Panels.MsgAddGroupTitle"), Loc.Get("Str.Panels.MsgGroupName"));
			if (String.IsNullOrWhiteSpace(name))
			{
				return;
			}

			FunctionOperationResult result = await this._catalog.AddCategoryAsync(parent.Node, name.Trim());
			if (await this.ReportAsync(result))
			{
				this._keyToSelect = parent.Key + "/" + name.Trim();
				this.Reload();
			}
		}

		/// <summary>Renames the selected category.</summary>
		[RelayCommand]
		public async Task RenameGroupAsync()
		{
			FunctionNodeViewModel? node = this.SelectedNode;
			if (node is null || !this._catalog.CanRenameCategory(node.Node))
			{
				return;
			}

			String? name = await this._dialogs.PromptAsync(Loc.Get("Str.Panels.MsgRenameGroupTitle"), Loc.Get("Str.Panels.MsgGroupName"), node.Name);
			if (String.IsNullOrWhiteSpace(name))
			{
				return;
			}

			FunctionOperationResult result = await this._catalog.RenameCategoryAsync(node.Node, name.Trim());
			if (await this.ReportAsync(result))
			{
				this._keyToSelect = (node.Parent is null ? String.Empty : node.Parent.Key + "/") + name.Trim();
				this.Reload();
			}
		}

		/// <summary>Deletes the selected function or empty category.</summary>
		[RelayCommand]
		public async Task DeleteNodeAsync()
		{
			FunctionNodeViewModel? node = this.SelectedNode;
			if (node is null || !this._catalog.CanDelete(node.Node))
			{
				return;
			}

			if (!await this._dialogs.ConfirmAsync(Loc.Get("Str.Common.Delete"), Loc.Format("Str.Panels.MsgConfirmDeleteNode", node.Name)))
			{
				return;
			}

			FunctionOperationResult result = await this._catalog.DeleteAsync(node.Node);
			if (await this.ReportAsync(result))
			{
				this._keyToSelect = node.Parent?.Key;
				this.Reload();
			}
		}

		/// <summary>Gets the text that tells the user why a change of the functions tree failed, in the language in use.</summary>
		/// <param name="result">The failed result.</param>
		/// <returns>The localized explanation.</returns>
		public static String DescribeFailure(FunctionOperationResult result)
		{
			ArgumentNullException.ThrowIfNull(result);
			return result.Error switch
			{
				FunctionOperationError.NotAllowed => Loc.Get("Str.Panels.MsgFunctionNotAllowed"),
				FunctionOperationError.InvalidDefinition => Loc.Get("Str.Panels.MsgFunctionInvalid"),
				FunctionOperationError.NodeChanged => Loc.Get("Str.Panels.MsgFunctionFileChanged"),
				FunctionOperationError.IoError => Loc.Format("Str.Panels.MsgFunctionIo", result.Message ?? String.Empty),
				_ => Loc.Get("Str.Panels.MsgOperationFailed"),
			};
		}

		private static void CollectExpanded(IEnumerable<FunctionNodeViewModel> nodes, HashSet<String> expanded)
		{
			foreach (FunctionNodeViewModel node in nodes)
			{
				if (node.IsExpanded)
				{
					expanded.Add(node.Key);
				}

				CollectExpanded(node.Children, expanded);
			}
		}

		private static void ApplyState(IEnumerable<FunctionNodeViewModel> nodes, HashSet<String> expanded, String? selectedKey, Boolean firstLoad, FunctionsPanelViewModel panel)
		{
			foreach (FunctionNodeViewModel node in nodes)
			{
				node.IsExpanded = !firstLoad && expanded.Contains(node.Key);
				if (selectedKey is not null && String.Equals(node.Key, selectedKey, StringComparison.Ordinal))
				{
					node.IsSelected = true;
					panel.SelectedNode = node;
					for (FunctionNodeViewModel? parent = node.Parent; parent is not null; parent = parent.Parent)
					{
						parent.IsExpanded = true;
					}
				}

				ApplyState(node.Children, expanded, selectedKey, firstLoad, panel);
			}
		}

		private void OnCatalogChanged(Object? sender, EventArgs e)
		{
			if (this._context is null)
			{
				this.Reload();
			}
			else
			{
				this._context.Post(_ => this.Reload(), null);
			}
		}

		private async Task<Boolean> ReportAsync(FunctionOperationResult result)
		{
			if (!result.Succeeded)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Panels.MsgFunctionsTitle"), DescribeFailure(result));
			}

			return result.Succeeded;
		}
	}
}