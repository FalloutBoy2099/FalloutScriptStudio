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
			if (this.AutoSaveChart && this._loaded && this._fcdPath is not null)
			{
				FlowchartDocument document = this.CreateDocument();
				String path = this._fcdPath;
				try
				{
					this._store.Save(path, document);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this._workspace.ReportFailure(Loc.Format("Str.Flowchart.Msg.SaveFailed", ex.Message));
				}
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
			DiagramNote note = new()
			{ X = position.X, Y = position.Y };
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

		/// <summary>Builds the nodes of the script; <see langword="null"/> when the script could not be parsed (so nothing may be replaced or saved from it).</summary>
		private async Task<FlowchartModel?> BuildModelAsync()
		{
			DocumentViewModel script = this.RequireScript();
			if (script.ProgramInfo is null || script.NeedsParse)
			{
				await this._workspace.RequestParseAsync(script, true);
			}

			ProgramInfo? info = script.ProgramInfo;
			if (info is null)
			{
				return null;
			}

			IDialogueMessageSource source = await this._messages.CreateSourceAsync(script);
			FlowchartBuildOptions options = new()
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
				FlowchartModel? model = await this.BuildModelAsync();
				if (model is null)
				{
					return;
				}

				this._model = model;
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
				this._loaded = true;
				this.RefreshEditingMarks();
				this.NeedUpdate = false;
				await this.RefreshQualityAsync();
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
				FlowchartModel? model = await this.BuildModelAsync();
				if (model is null)
				{
					return;
				}

				this._model = model;
				this.ReplaceNodes(newNodeName);
				this.RefreshEditingMarks();
				this.NeedUpdate = false;
				await this.RefreshQualityAsync();
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
			Dictionary<String, DiagramNode> existing = new(StringComparer.OrdinalIgnoreCase);
			foreach (DiagramNode node in canvas.Nodes)
			{
				existing.TryAdd(node.Name, node);
			}

			HashSet<DiagramNode> replaced = [];
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
	}
}