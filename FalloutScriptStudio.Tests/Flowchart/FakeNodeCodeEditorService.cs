namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	/// <summary>Tracks the "open" node editors without any window.</summary>
	internal sealed class FakeNodeCodeEditorService : INodeCodeEditorService
	{
		private readonly List<String> _open = [];

		public event EventHandler<NodeEditorEventArgs>? EditorsChanged;

		public event EventHandler<NodeEditorEventArgs>? CodeApplied;

		public Int32 CloseCount { get; private set; }

		public Int32 CloseAllCount { get; private set; }

		public List<(String Node, Boolean FromFlowchart)> Opened { get; } = [];

		public Boolean IsEditing(DocumentViewModel script, String nodeName)
		{
			return this._open.Contains(nodeName, StringComparer.OrdinalIgnoreCase);
		}

		public IReadOnlyList<String> GetEditedNodes(DocumentViewModel script)
		{
			return this._open.ToList();
		}

		public Boolean Open(DocumentViewModel script, String nodeName, Boolean fromFlowchart)
		{
			this.Opened.Add((nodeName, fromFlowchart));
			if (!this.IsEditing(script, nodeName))
			{
				this._open.Add(nodeName);
			}

			this.EditorsChanged?.Invoke(this, new NodeEditorEventArgs(script, nodeName));
			return true;
		}

		public void CloseFlowchartEditors(DocumentViewModel script)
		{
			this.CloseCount++;
			this._open.Clear();
		}

		public Task CloseEditorsAsync(DocumentViewModel script)
		{
			this.CloseAllCount++;
			this._open.Clear();
			return Task.CompletedTask;
		}

		public void RaiseApplied(DocumentViewModel script, String nodeName)
		{
			this.CodeApplied?.Invoke(this, new NodeEditorEventArgs(script, nodeName));
		}
	}
}