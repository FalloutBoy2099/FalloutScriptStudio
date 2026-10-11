namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using FalloutScriptStudio.Views.Flowchart;
	using FalloutScriptStudio.Views.ScriptTools;
	using Microsoft.Extensions.DependencyInjection;

	/// <inheritdoc />
	public sealed class NodeCodeEditorService : INodeCodeEditorService
	{
		private readonly IServiceProvider _services;
		private readonly List<Entry> _editors = new List<Entry>();

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeCodeEditorService"/> class.
		/// </summary>
		/// <param name="services">Creates the editor windows.</param>
		public NodeCodeEditorService(IServiceProvider services)
		{
			ArgumentNullException.ThrowIfNull(services);
			this._services = services;
		}

		/// <inheritdoc />
		public event EventHandler<NodeEditorEventArgs>? EditorsChanged;

		/// <inheritdoc />
		public event EventHandler<NodeEditorEventArgs>? CodeApplied;

		/// <inheritdoc />
		public Boolean IsEditing(DocumentViewModel script, String nodeName)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(nodeName);
			return this.Find(script, nodeName) is not null;
		}

		/// <inheritdoc />
		public IReadOnlyList<String> GetEditedNodes(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);
			return this._editors.Where(e => ReferenceEquals(e.Script, script)).Select(e => e.Window.ViewModel.NodeName).ToList();
		}

		/// <inheritdoc />
		public Boolean Open(DocumentViewModel script, String nodeName, Boolean fromFlowchart)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(nodeName);

			Entry? existing = this.Find(script, nodeName);
			if (existing is not null)
			{
				if (existing.Window.WindowState == WindowState.Minimized)
				{
					existing.Window.WindowState = WindowState.Normal;
				}

				existing.Window.Activate();
				return true;
			}

			// One scope per window: the container tracks the disposable transient window and view model until the scope ends, so a
			// root-resolved editor would stay in memory (with its text document and script) for the whole session.
			IServiceScope scope = this._services.CreateScope();
			NodeCodeEditorWindow window = scope.ServiceProvider.GetRequiredService<NodeCodeEditorWindow>();
			Window? owner = ScriptToolWindowHelper.FindOwner(window);
			Entry entry = new Entry(script, window, fromFlowchart, nodeName);
			window.ViewModel.CloseOnDone = fromFlowchart;
			this._editors.Add(entry);
			window.ViewModel.CodeApplied += (_, _) => this.CodeApplied?.Invoke(this, new NodeEditorEventArgs(script, window.ViewModel.NodeName));
			window.ViewModel.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(NodeCodeEditorViewModel.NodeName))
				{
					this.EditorsChanged?.Invoke(this, new NodeEditorEventArgs(script, window.ViewModel.NodeName));
				}
			};
			window.Closed += (_, _) =>
			{
				this._editors.Remove(entry);
				this.EditorsChanged?.Invoke(this, new NodeEditorEventArgs(script, window.ViewModel.NodeName));
				scope.Dispose();
			};

			window.Show();
			_ = InitializeAsync(entry, owner);
			this.EditorsChanged?.Invoke(this, new NodeEditorEventArgs(script, nodeName));
			return true;
		}

		/// <inheritdoc />
		public void CloseFlowchartEditors(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);

			foreach (Entry entry in this._editors.Where(e => ReferenceEquals(e.Script, script) && e.FromFlowchart).ToList())
			{
				entry.Window.ForceClose = true;
				entry.Window.Close();
			}
		}

		/// <inheritdoc />
		public async Task CloseEditorsAsync(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);

			foreach (Entry entry in this._editors.Where(e => ReferenceEquals(e.Script, script)).ToList())
			{
				await entry.Window.CloseAsync();
			}
		}

		private static async Task InitializeAsync(Entry entry, Window? owner)
		{
			try
			{
				await entry.Window.InitializeAsync(entry.Script, entry.OpenedNode, owner);
			}
			catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
			{
				entry.Window.ForceClose = true;
				entry.Window.Close();
			}
		}

		private Entry? Find(DocumentViewModel script, String nodeName)
		{
			return this._editors.FirstOrDefault(e => ReferenceEquals(e.Script, script)
				&& (String.Equals(e.Window.ViewModel.NodeName, nodeName, StringComparison.OrdinalIgnoreCase)
					|| (e.Window.ViewModel.NodeName.Length == 0 && String.Equals(e.OpenedNode, nodeName, StringComparison.OrdinalIgnoreCase))));
		}

		private sealed record Entry(DocumentViewModel Script, NodeCodeEditorWindow Window, Boolean FromFlowchart, String OpenedNode);
	}
}
