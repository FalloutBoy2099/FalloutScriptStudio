namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Controls.Diagram;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	public sealed partial class FlowchartViewModel
	{
		/// <summary>
		/// Shows the flowchart of the script: loads the flowchart file next to the script (or in its <c>fcd</c> folder) when one
		/// exists, otherwise creates a new chart from the nodes of the script.
		/// </summary>
		/// <returns>A task that completes when the chart is shown.</returns>
		public async Task LoadAsync()
		{
			DocumentViewModel script = this.RequireScript();
			if (script.FilePath is not null)
			{
				String? file = this._store.FindFile(script.FilePath);
				if (file is not null)
				{
					this._fcdPath = file;
					if (await this.LoadFileAsync(file))
					{
						return;
					}
				}
			}

			await this.CreateGraphAsync();
		}

		/// <summary>
		/// Reads the nodes of the script again and replaces the nodes of the canvas, keeping the position, size and state of the
		/// nodes that still exist (legacy <c>UpdateAllNodes</c>).
		/// </summary>
		/// <returns>A task that completes when the canvas is up to date.</returns>
		[RelayCommand]
		public Task UpdateAsync()
		{
			return this.UpdateNodesAsync(null);
		}

		/// <summary>Handles the activation of the window: updates the chart when the script changed (or tells the user so).</summary>
		/// <returns>A task that completes when the update is done.</returns>
		public async Task OnActivatedAsync()
		{
			if (this._script is null || !this.NeedUpdate)
			{
				return;
			}

			if (this.AutoUpdate)
			{
				await this.UpdateNodesAsync(null);
			}
			else
			{
				this.ShowHint(Loc.Get("Str.Flowchart.Hint.Outdated"), OutdatedHintSeconds);
			}
		}

		/// <summary>Handles the closing of the window: closes the node editors opened from it and saves the chart when asked.</summary>
		public void OnClosing()
		{
			if (this._script is null || this._canvas is null)
			{
				return;
			}

			this._nodeEditors.CloseFlowchartEditors(this._script);
			if (this.AutoSaveChart && this._fcdPath is not null)
			{
				FlowchartDocument document = this.CreateDocument();
				String path = this._fcdPath;
				try
				{
					this._store.Save(path, document);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this._workspace.ReportStatus(Loc.Format("Str.Flowchart.Msg.SaveFailed", ex.Message));
				}
			}
		}

		/// <summary>Creates the flowchart file content from the canvas.</summary>
		/// <returns>The document.</returns>
		public FlowchartDocument CreateDocument()
		{
			DiagramCanvas canvas = this.RequireCanvas();
			String scriptName = FlowchartDocument.CreateScriptName(this.RequireScript().FilePath ?? DocumentViewModel.UnsavedFileName);
			return FlowchartDiagramMapper.CreateDocument(canvas.Items, scriptName, canvas.Zoom, (Int32)canvas.HorizontalOffset, (Int32)canvas.VerticalOffset);
		}

		/// <summary>Cleans the canvas and creates a new flowchart from the nodes of the script (asks first when the canvas is not empty).</summary>
		/// <returns>A task that completes when the chart is created.</returns>
		[RelayCommand]
		public async Task CreateAsync()
		{
			DiagramCanvas canvas = this.RequireCanvas();
			if (canvas.Nodes.Any()
				&& !await this._dialogs.ConfirmAsync(Loc.Get("Str.Flowchart.Msg.CreateNew.Title"), Loc.Get("Str.Flowchart.Msg.CreateNew.Text")))
			{
				return;
			}

			await this.CreateGraphAsync();
			this.ShowHint(Loc.Get("Str.Flowchart.Hint.NewFlowchart"), NewFlowchartHintSeconds);
		}

		/// <summary>Opens a flowchart file chosen by the user.</summary>
		/// <returns>A task that completes when the file is loaded.</returns>
		[RelayCommand]
		public async Task OpenFlowchartAsync()
		{
			DocumentViewModel script = this.RequireScript();
			String? directory = Path.GetDirectoryName(this._fcdPath ?? script.FilePath);
			String? path = this._dialogs.PickOpenFile(Loc.Get("Str.Flowchart.Dlg.OpenTitle"), CreateFilter("Str.Flowchart.Filter.Fcd", FileFilterPattern), directory);
			if (path is null)
			{
				return;
			}

			String? previous = this._fcdPath;
			this._fcdPath = path;
			if (!await this.LoadFileAsync(path))
			{
				this._fcdPath = previous;
			}
		}

		/// <summary>Saves the flowchart to its file; asks for the file the first time.</summary>
		/// <returns>A task that completes when the file is written.</returns>
		[RelayCommand]
		public async Task SaveFlowchartAsync()
		{
			DocumentViewModel script = this.RequireScript();
			if (this._fcdPath is null)
			{
				String name = Path.ChangeExtension(FlowchartDocument.CreateScriptName(script.FilePath ?? DocumentViewModel.UnsavedFileName), FlowchartDocumentSerializer.FileExtension);
				String? path = this._dialogs.PickSaveFile(Loc.Get("Str.Flowchart.Dlg.SaveTitle"), CreateFilter("Str.Flowchart.Filter.Fcd", FileFilterPattern), Path.GetDirectoryName(script.FilePath), name);
				if (path is null)
				{
					return;
				}

				this._fcdPath = path;
			}

			try
			{
				await this._store.SaveAsync(this._fcdPath, this.CreateDocument());
				this._workspace.ReportStatus(Loc.Format("Str.Flowchart.Status.Saved", this._fcdPath));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Menu.File.Save"), Loc.Format("Str.Flowchart.Msg.SaveFailed", ex.Message));
			}
		}

		/// <summary>Saves the whole flowchart as a PNG image.</summary>
		/// <returns>A task that completes when the image is written.</returns>
		[RelayCommand]
		public async Task SaveImageAsync()
		{
			DiagramCanvas canvas = this.RequireCanvas();
			String? path = this._dialogs.PickSaveFile(Loc.Get("Str.Flowchart.Menu.File.SaveImage"), CreateFilter("Str.Flowchart.Filter.Png", ImageFilterPattern), Path.GetDirectoryName(this.RequireScript().FilePath), null);
			if (path is null)
			{
				return;
			}

			try
			{
				canvas.SaveToPng(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Menu.File.SaveImage"), Loc.Format("Str.Flowchart.Msg.ImageSaveFailed", ex.Message));
			}
		}

		/// <summary>Collapses all nodes to their header, or all message rows when <paramref name="rows"/> is set (legacy Shift).</summary>
		/// <param name="rows">Whether the message rows are collapsed instead of the nodes.</param>
		[RelayCommand]
		public void CollapseAll(Boolean rows)
		{
			DiagramCanvas canvas = this.RequireCanvas();
			if (rows)
			{
				canvas.CollapseAllRows();
			}
			else
			{
				canvas.CollapseAllNodes();
			}
		}

		/// <summary>Expands all nodes, or all message rows when <paramref name="rows"/> is set (legacy Shift).</summary>
		/// <param name="rows">Whether the message rows are expanded instead of the nodes.</param>
		[RelayCommand]
		public void ExpandAll(Boolean rows)
		{
			DiagramCanvas canvas = this.RequireCanvas();
			if (rows)
			{
				canvas.ExpandAllRows();
			}
			else
			{
				canvas.ExpandAllNodes();
			}
		}

		/// <summary>Gives every node the width of its content, at most the usual limit.</summary>
		[RelayCommand]
		public void ShrinkAllWidths()
		{
			this.RequireCanvas().ShrinkAllWidths();
		}

		/// <summary>Gives every node the width of its content.</summary>
		[RelayCommand]
		public void MatchAllWidths()
		{
			this.RequireCanvas().MatchAllWidths();
		}

		/// <summary>Arranges the visible nodes in layers following their links.</summary>
		[RelayCommand]
		public void Arrange()
		{
			this.RequireCanvas().AutoArrange();
		}

		/// <summary>Opens the configuration of the dialogue function rules and updates the chart afterwards.</summary>
		/// <returns>A task that completes when the configuration is closed.</returns>
		[RelayCommand]
		public async Task ShowFunctionRulesAsync()
		{
			await this._tools.ShowFunctionRulesAsync();
			await this.UpdateNodesAsync(null);
		}

		/// <summary>Adds a free text note in the top left corner of the visible area.</summary>
		[RelayCommand]
		public void AddNote()
		{
			DiagramCanvas canvas = this.RequireCanvas();
			Point position = canvas.ViewportToWorld(new Point(40, 40));
			DiagramNote note = new DiagramNote { X = position.X, Y = position.Y };
			canvas.AddItem(note);
			canvas.ClearSelection();
			canvas.Select(note);
		}

		/// <summary>Moves the view to a node, showing it first when it is hidden, and selects it.</summary>
		/// <param name="name">The node name.</param>
		[RelayCommand]
		public void JumpToNode(String? name)
		{
			DiagramCanvas canvas = this.RequireCanvas();
			DiagramNode? node = name is null ? null : canvas.FindNode(name);
			if (node is null)
			{
				return;
			}

			if (node.IsHidden)
			{
				this.UnhideNode(node.Name);
			}

			canvas.ClearSelection();
			canvas.Select(node);
			canvas.ScrollToItem(node);
		}

		private IReadOnlyList<String> _missingMessageFiles = Array.Empty<String>();

		private async Task<FlowchartModel> BuildModelAsync()
		{
			DocumentViewModel script = this.RequireScript();
			if (script.ProgramInfo is null || script.NeedsParse)
			{
				await this._workspace.RequestParseAsync(script, true);
			}

			ProgramInfo? info = script.ProgramInfo;
			if (info is null)
			{
				return FlowchartModel.Empty;
			}

			IDialogueMessageSource source = await this._messages.CreateSourceAsync(script);
			FlowchartBuildOptions options = new FlowchartBuildOptions
			{
				ExcludeExitNode = this.WithoutExitNode,
				ShowCommentedCode = this.ShowCommentCode,
				TabSize = this._settings.Settings.Editor.TabSize,
			};
			FlowchartModel model = await this._builder.BuildAsync(script.TextDocument.Text, info, source, options);
			this._missingMessageFiles = MissingMessageFiles.Find(model, source);
			return model;
		}

		/// <summary>Tells the user about the message files of the dialogue that were not found, one message per file (legacy <c>ShowMissingFiles</c>).</summary>
		private async Task ReportMissingMessageFilesAsync()
		{
			IReadOnlyList<String> missing = this._missingMessageFiles;
			this._missingMessageFiles = Array.Empty<String>();
			foreach (String path in missing)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Msg.MissingDialogueFile.Title"), Loc.Format("Str.Flowchart.Msg.MissingDialogueFile.Text", path));
			}
		}

		private async Task CreateGraphAsync()
		{
			DiagramCanvas canvas = this.RequireCanvas();
			this._selfChange++;
			try
			{
				this._model = await this.BuildModelAsync();
				canvas.Clear();
				Int32 shiftX = 0;
				Int32 shiftY = 0;
				foreach (FlowchartNodeData data in this._model.Nodes)
				{
					this.AddNewNode(data, ref shiftX, ref shiftY);
				}

				if (this.AutoArrange)
				{
					canvas.AutoArrange();
				}

				canvas.Refresh();
				this.RefreshEditingMarks();
				this.NeedUpdate = false;
			}
			finally
			{
				this._selfChange--;
			}

			await this.ReportMissingMessageFilesAsync();
		}

		/// <summary>Adds a node that has no saved position, below the previous one (legacy <c>AddNodeToCanvas</c>).</summary>
		private void AddNewNode(FlowchartNodeData data, ref Int32 shiftX, ref Int32 shiftY)
		{
			DiagramNode node = FlowchartDiagramMapper.CreateNode(data, this.ShowAllCode);
			if (this.AutoHideNodes && data.Kind == DialogueNodeKind.Unused)
			{
				node.IsHidden = true;
				this.RequireCanvas().AddItem(node);
				return;
			}

			node.X = 25 + shiftX;
			node.Y = 25 + shiftY;
			this.RequireCanvas().AddItem(node);
			Int32 height = (Int32)node.Height;
			if (height > 500)
			{
				shiftX += 50 + (Int32)node.Width;
			}
			else
			{
				shiftY += 25 + height;
				shiftX += 25 + ((Int32)node.Width / 2);
			}
		}

		private async Task UpdateNodesAsync(String? newNodeName)
		{
			if (this._script is null || this._canvas is null || this._busy)
			{
				return;
			}

			this._busy = true;
			this._selfChange++;
			try
			{
				this._model = await this.BuildModelAsync();
				this.ReplaceNodes(newNodeName);
				this.RefreshEditingMarks();
				this.NeedUpdate = false;
			}
			finally
			{
				this._selfChange--;
				this._busy = false;
			}
		}

		private void ReplaceNodes(String? newNodeName)
		{
			DiagramCanvas canvas = this.RequireCanvas();
			Dictionary<String, DiagramNode> existing = new Dictionary<String, DiagramNode>(StringComparer.OrdinalIgnoreCase);
			foreach (DiagramNode node in canvas.Nodes)
			{
				existing.TryAdd(node.Name, node);
			}

			HashSet<DiagramNode> replaced = new HashSet<DiagramNode>();
			Int32 shift = 0;
			foreach (FlowchartNodeData data in this._model.Nodes)
			{
				DiagramNode node = FlowchartDiagramMapper.CreateNode(data, this.ShowAllCode);
				if (existing.TryGetValue(data.Name, out DiagramNode? old))
				{
					FlowchartDiagramMapper.ApplyState(node, FlowchartDiagramMapper.CaptureState(old));
					canvas.ReplaceItem(old, node);
					replaced.Add(old);
				}
				else if (String.Equals(data.Name, newNodeName, StringComparison.OrdinalIgnoreCase))
				{
					Point center = canvas.GetViewportCenterInWorld();
					canvas.AddItem(node);
					node.X = center.X - (node.Width / 2);
					node.Y = center.Y - (node.Height / 2);
					canvas.ClearSelection();
					canvas.Select(node);
				}
				else
				{
					node.X = 100 + shift;
					node.Y = 100;
					shift += 350;
					canvas.AddItem(node);
				}
			}

			foreach (DiagramNode old in existing.Values.Where(n => !replaced.Contains(n)).ToList())
			{
				canvas.RemoveItem(old);
			}
		}

		private async Task<Boolean> LoadFileAsync(String path)
		{
			DocumentViewModel script = this.RequireScript();
			DiagramCanvas canvas = this.RequireCanvas();
			FlowchartDocument document;
			try
			{
				document = await this._store.LoadAsync(path);
			}
			catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Menu.File.Open"), Loc.Format("Str.Flowchart.Msg.OpenFailed", ex.Message));
				return false;
			}

			if (!document.IsForScript(script.FilePath ?? DocumentViewModel.UnsavedFileName))
			{
				await this._dialogs.ShowWarningAsync(Loc.Format("Str.Flowchart.Msg.WrongFlowchart.Title", document.ScriptName), Loc.Get("Str.Flowchart.Msg.WrongFlowchart.Text"));
				return false;
			}

			this._selfChange++;
			try
			{
				this._model = await this.BuildModelAsync();
				FlowchartReconciliation reconciliation = document.Reconcile(this._model.NodeNames);
				canvas.Clear();
				foreach (FlowchartNodeState state in reconciliation.Document.Nodes)
				{
					FlowchartNodeData? data = this._model.FindNode(state.Name);
					if (data is null)
					{
						continue;
					}

					DiagramNode node = FlowchartDiagramMapper.CreateNode(data, this.ShowAllCode);
					FlowchartDiagramMapper.ApplyState(node, state);
					canvas.AddItem(node);
				}

				foreach (FlowchartNoteState note in document.Notes)
				{
					canvas.AddItem(FlowchartDiagramMapper.CreateNote(note));
				}

				Int32 shiftX = 0;
				Int32 shiftY = 0;
				foreach (String added in reconciliation.AddedNodes)
				{
					FlowchartNodeData? data = this._model.FindNode(added);
					if (data is not null)
					{
						this.AddNewNode(data, ref shiftX, ref shiftY);
					}
				}

				canvas.Zoom = document.Zoom > 0 ? document.Zoom : 1.0;
				canvas.ScrollToOffset(document.PositionX, document.PositionY);
				this.RefreshEditingMarks();
				this.NeedUpdate = false;
				await this.ReportReconciliationAsync(reconciliation);
				await this.ReportMissingMessageFilesAsync();
				return true;
			}
			finally
			{
				this._selfChange--;
			}
		}

		private async Task ReportReconciliationAsync(FlowchartReconciliation reconciliation)
		{
			if (reconciliation.AddedNodes.Count > 0)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Common.Loading"), Loc.Format("Str.Flowchart.Msg.NodesAdded", String.Join(", ", reconciliation.AddedNodes)));
			}

			if (reconciliation.RemovedNodes.Count > 0)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Common.Loading"), Loc.Format("Str.Flowchart.Msg.NodesRemoved", String.Join(", ", reconciliation.RemovedNodes)));
			}
		}
	}
}
