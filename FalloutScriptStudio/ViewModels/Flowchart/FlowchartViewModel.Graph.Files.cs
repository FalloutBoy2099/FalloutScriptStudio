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

	/// <summary>Creating, loading and saving the flowchart file and the image of the chart.</summary>
	public sealed partial class FlowchartViewModel
	{
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

			await this.LoadFileAsync(path);
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
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException or ArgumentException or InvalidOperationException or NotSupportedException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Flowchart.Menu.File.SaveImage"), Loc.Format("Str.Flowchart.Msg.ImageSaveFailed", ex.Message));
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
				FlowchartModel? model = await this.BuildModelAsync();
				if (model is null)
				{
					return false;
				}

				this._model = model;
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
				this._fcdPath = path;
				this._loaded = true;
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