namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using FalloutScriptStudio.Controls.Diagram.Model;

	internal sealed class SceneChangedEventArgs : EventArgs
	{
		public SceneChangedEventArgs(SceneChange change, DiagramItem? item)
		{
			this.Change = change;
			this.Item = item;
		}

		public SceneChange Change { get; }

		public DiagramItem? Item { get; }
	}
}
