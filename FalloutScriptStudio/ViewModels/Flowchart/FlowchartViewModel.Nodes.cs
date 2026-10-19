namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	public sealed partial class FlowchartViewModel
	{
		/// <summary>Gets the node the context menu was opened on (the node under the pointer), or <see langword="null"/>.</summary>
		public DiagramNode? ContextNode { get; private set; }

		/// <summary>Gets the nodes the selected node links to.</summary>
		public IReadOnlyList<FlowchartJumpTarget> JumpToTargets { get; private set; } = Array.Empty<FlowchartJumpTarget>();

		/// <summary>Gets the nodes that link to the selected node.</summary>
		public IReadOnlyList<FlowchartJumpTarget> JumpFromTargets { get; private set; } = Array.Empty<FlowchartJumpTarget>();

		/// <summary>Gets the names of the hidden nodes.</summary>
		public IReadOnlyList<String> HiddenNodeNames { get; private set; } = Array.Empty<String>();

		/// <summary>Gets or sets whether the edit command can run.</summary>
		[ObservableProperty]
		public partial Boolean CanEditNode { get; set; }

		/// <summary>Gets or sets whether the rename command can run.</summary>
		[ObservableProperty]
		public partial Boolean CanRenameNode { get; set; }

		/// <summary>Gets or sets whether the dialogue preview can be opened from the node.</summary>
		[ObservableProperty]
		public partial Boolean CanPreviewNode { get; set; }

		/// <summary>Gets or sets whether the hide command can run.</summary>
		[ObservableProperty]
		public partial Boolean CanHideSelection { get; set; }

		/// <summary>Gets or sets whether the delete command can run.</summary>
		[ObservableProperty]
		public partial Boolean CanDeleteSelection { get; set; }

		/// <summary>Gets or sets whether the jump menu has a node to jump from.</summary>
		[ObservableProperty]
		public partial Boolean CanJump { get; set; }

		/// <summary>Gets or sets the text of the edit command.</summary>
		[ObservableProperty]
		public partial String EditNodeHeader { get; set; } = Loc.Get("Str.Flowchart.Menu.Node.Edit");

		/// <summary>Gets or sets the text of the preview command.</summary>
		[ObservableProperty]
		public partial String PreviewNodeHeader { get; set; } = Loc.Get("Str.Flowchart.Menu.Node.Preview");

		/// <summary>Gets or sets the text of the rename command.</summary>
		[ObservableProperty]
		public partial String RenameNodeHeader { get; set; } = Loc.Get("Str.Flowchart.Menu.Node.Rename");

		/// <summary>Gets or sets the text of the jump menu.</summary>
		[ObservableProperty]
		public partial String JumpHeader { get; set; } = Loc.Get("Str.Flowchart.Menu.Node.Jump");

		/// <summary>
		/// Works out what the context menu can do. Call it when the menu opens.
		/// </summary>
		/// <param name="hovered">The item under the pointer, or <see langword="null"/>.</param>
		public void PrepareContext(DiagramItem? hovered)
		{
			DiagramCanvas canvas = this.RequireCanvas();
			DocumentViewModel script = this.RequireScript();
			DiagramNode? node = hovered as DiagramNode;
			this.ContextNode = node;

			this.CanEditNode = node is not null;
			this.EditNodeHeader = node is null ? Loc.Get("Str.Flowchart.Menu.Node.Edit") : Loc.Format("Str.Flowchart.Menu.Node.EditNamed", node.Name);
			this.CanPreviewNode = node is not null;
			this.PreviewNodeHeader = node is null ? Loc.Get("Str.Flowchart.Menu.Node.Preview") : Loc.Format("Str.Flowchart.Menu.Node.PreviewNamed", node.Name);
			this.CanRenameNode = node is not null && !IsTalkProcedure(node.Name) && !this._nodeEditors.IsEditing(script, node.Name);
			this.RenameNodeHeader = node is null ? Loc.Get("Str.Flowchart.Menu.Node.Rename") : Loc.Format("Str.Flowchart.Menu.Node.RenameNamed", node.Name);

			List<DiagramNode> selectedNodes = canvas.SelectedItems.OfType<DiagramNode>().ToList();
			this.CanHideSelection = selectedNodes.Count > 0;
			this.CanDeleteSelection = canvas.SelectedItems.Count > 0;
			this.HiddenNodeNames = canvas.Nodes.Where(n => n.IsHidden).Select(n => n.Name).ToList();

			DiagramNode? jumpNode = canvas.SelectedItem as DiagramNode ?? node;
			FlowchartNodeData? data = jumpNode is null ? null : this._model.FindNode(jumpNode.Name);
			this.CanJump = jumpNode is not null;
			this.JumpHeader = jumpNode is null ? Loc.Get("Str.Flowchart.Menu.Node.Jump") : Loc.Format("Str.Flowchart.Menu.Node.JumpNamed", jumpNode.Name);
			this.JumpToTargets = data is null
				? Array.Empty<FlowchartJumpTarget>()
				: data.LinkedTo.Select(l => FlowchartDiagramMapper.NormalizeTarget(l.TargetNode)).Distinct(StringComparer.OrdinalIgnoreCase).Select(this.CreateJumpTarget).ToList();
			this.JumpFromTargets = data is null
				? Array.Empty<FlowchartJumpTarget>()
				: data.LinkedFrom.Distinct(StringComparer.OrdinalIgnoreCase).Select(this.CreateJumpTarget).ToList();
		}

		/// <summary>Opens the code editor of the node under the pointer (or the selected node).</summary>
		[RelayCommand]
		public void EditNode()
		{
			DiagramNode? node = this.ContextNode ?? this._canvas?.SelectedItem as DiagramNode;
			if (node is not null && this._script is not null)
			{
				this._nodeEditors.Open(this._script, node.Name, true);
			}
		}

		/// <summary>Opens the dialogue preview at the node under the pointer (or the selected node); a preview window that is already open moves to it.</summary>
		/// <returns>A task that completes when the preview is shown.</returns>
		[RelayCommand]
		public async Task PreviewNodeAsync()
		{
			DiagramNode? node = this.ContextNode ?? this._canvas?.SelectedItem as DiagramNode;
			if (node is not null && this._script is not null)
			{
				await this._workspace.PreviewDialogueFromNodeAsync(this._script, node.Name);
			}
		}

		/// <summary>Renames the node under the pointer with the rename refactoring; every reference changes.</summary>
		/// <returns>A task that completes when the node is renamed.</returns>
		[RelayCommand]
		public async Task RenameNodeAsync()
		{
			DiagramNode? node = this.ContextNode ?? this._canvas?.SelectedItem as DiagramNode;
			DocumentViewModel script = this.RequireScript();
			if (node is null || IsTalkProcedure(node.Name) || this._nodeEditors.IsEditing(script, node.Name))
			{
				return;
			}

			NodeRenameResult result;
			this._selfChange++;
			try
			{
				result = await this._scriptEditor.RenameNodeAsync(script, node.Name);
			}
			finally
			{
				this._selfChange--;
			}

			switch (result.Outcome)
			{
				case NodeRenameOutcome.Renamed:
					node.Name = result.NewName ?? node.Name;
					await this.UpdateNodesAsync(null);
					break;
				case NodeRenameOutcome.Rejected:
					await this._dialogs.ShowWarningAsync(Loc.Get("Str.Flowchart.Menu.Node.Rename"), Loc.Get("Str.Flowchart.Msg.RenameRejected"));
					await this.UpdateNodesAsync(null);
					break;
				default:
					break;
			}
		}

		/// <summary>Asks for a new dialogue node and adds it in the middle of the view.</summary>
		/// <returns>A task that completes when the dialog is closed.</returns>
		[RelayCommand]
		public async Task AddNodeAsync()
		{
			String? name = await this._flowchartDialogs.ShowNewNodeAsync(this.RequireScript());
			if (name is not null)
			{
				await this.UpdateNodesAsync(name);
			}
		}

		/// <summary>
		/// Deletes the selected notes and nodes after asking. Deleting a node deletes its procedure from the script. The start node
		/// and the nodes that are being edited are never deleted.
		/// </summary>
		/// <returns>A task that completes when everything is deleted.</returns>
		[RelayCommand]
		public async Task DeleteSelectionAsync()
		{
			DiagramCanvas canvas = this.RequireCanvas();
			DocumentViewModel script = this.RequireScript();
			List<DiagramItem> selection = canvas.SelectedItems.ToList();
			if (selection.Count == 0)
			{
				return;
			}

			if (!await this._dialogs.ConfirmAsync(Loc.Get("Str.Flowchart.Msg.Deleting.Title"), Loc.Get("Str.Flowchart.Msg.ConfirmDelete")))
			{
				return;
			}

			List<String> procedures = [];
			Boolean refusedStart = false;
			List<String> refusedEdited = [];
			foreach (DiagramItem item in selection)
			{
				if (item is DiagramNode node)
				{
					if (IsTalkProcedure(node.Name))
					{
						refusedStart = true;
					}
					else if (this._nodeEditors.IsEditing(script, node.Name))
					{
						refusedEdited.Add(node.Name);
					}
					else
					{
						canvas.RemoveItem(node);
						procedures.Add(node.Name);
					}
				}
				else
				{
					canvas.RemoveItem(item);
				}
			}

			if (refusedStart)
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Flowchart.Msg.Deleting.Title"), Loc.Format("Str.Flowchart.Msg.CannotDeleteStart", TalkProcedure));
			}

			if (refusedEdited.Count > 0)
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Flowchart.Msg.Deleting.Title"), Loc.Format("Str.Flowchart.Msg.CannotDeleteEdited", String.Join(", ", refusedEdited)));
			}

			if (procedures.Count > 0)
			{
				this._selfChange++;
				try
				{
					await this._scriptEditor.DeleteNodesAsync(script, procedures);
				}
				finally
				{
					this._selfChange--;
				}
			}

			canvas.ClearSelection();
			await this.UpdateNodesAsync(null);
		}

		/// <summary>Hides the selected nodes; they stay in the script and in the saved flowchart.</summary>
		[RelayCommand]
		public void HideSelection()
		{
			DiagramCanvas canvas = this.RequireCanvas();
			List<DiagramNode> nodes = canvas.SelectedItems.OfType<DiagramNode>().ToList();
			canvas.ClearSelection();
			foreach (DiagramNode node in nodes)
			{
				node.IsHidden = true;
			}
		}

		/// <summary>Shows a hidden node again.</summary>
		/// <param name="name">The node name.</param>
		[RelayCommand]
		public void UnhideNode(String? name)
		{
			DiagramNode? node = name is null ? null : this.RequireCanvas().FindNode(name);
			if (node is null || !node.IsHidden)
			{
				return;
			}

			if (node.X == 0 && node.Y == 0)
			{
				node.X = 25;
				node.Y = 25;
			}

			node.IsHidden = false;
			this.RequireCanvas().BringToFront(node);
		}

		/// <summary>Shows all hidden nodes again.</summary>
		[RelayCommand]
		public void UnhideAll()
		{
			Double shift = 20;
			foreach (DiagramNode node in this.RequireCanvas().Nodes.Where(n => n.IsHidden).ToList())
			{
				if (node.X == 0 && node.Y == 0)
				{
					node.X = shift;
					node.Y = 20;
					shift += node.Width + 20;
				}

				node.IsHidden = false;
			}

			this.RequireCanvas().ClearSelection();
		}

		/// <summary>
		/// Lets the user edit the text of the message of a row (legacy Shift + click) and writes it to the message file.
		/// </summary>
		/// <param name="row">The row that was clicked.</param>
		/// <returns>A task that completes when the dialog is closed.</returns>
		public async Task QuickEditMessageAsync(DiagramRow row)
		{
			ArgumentNullException.ThrowIfNull(row);

			if (row.Tag is not FlowchartContentLine line || !line.Opcode.HasMessage() || line.MessageNumber <= 0)
			{
				return;
			}

			IDialogueMessageSource source = await this._messages.CreateSourceAsync(this.RequireScript());
			String? path = source.GetMessageFilePath(line.MessageFileNumber);
			if (path is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MissingMessagesFile.Title"), Loc.Get("Str.Flowchart.Msg.MissingMessagesFile.Text"));
				return;
			}

			DialogueMessageLookup lookup = source.GetMessage(line.MessageFileNumber, line.MessageNumber);
			if (!lookup.IsFound || lookup.Text is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MessageMissing.Title"), Loc.Format("Str.Flowchart.Msg.MessageMissing.Text", path));
				return;
			}

			String? text = await this._flowchartDialogs.EditMessageTextAsync(Loc.Format("Str.Flowchart.MessageFileCaption", path), lookup.Text);
			if (text is null)
			{
				return;
			}

			if (await this._messages.UpdateMessageTextAsync(path, line.MessageNumber, text))
			{
				FlowchartContentLine updated = line with { MessageText = text };
				row.Tag = updated;
				row.Text = FlowchartDiagramMapper.GetRowText(updated);
			}
			else
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MessageFile.Title"), Loc.Format("Str.Flowchart.Msg.MessageSaveFailed", path));
			}
		}

		private static Boolean IsTalkProcedure(String name)
		{
			return String.Equals(name, TalkProcedure, StringComparison.OrdinalIgnoreCase);
		}

		private FlowchartJumpTarget CreateJumpTarget(String name)
		{
			DiagramNode? node = this._canvas?.FindNode(name);
			return new FlowchartJumpTarget(name, node?.IsHidden ?? false);
		}
	}
}