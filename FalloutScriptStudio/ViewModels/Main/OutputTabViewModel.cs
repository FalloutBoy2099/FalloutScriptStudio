namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Workspace;

	/// <summary>A tab of the output panel. The view picks the template of a tab from its type; the tab reaches the panel for the data it shows.</summary>
	public abstract class OutputTabViewModel : ObservableObject
	{
		/// <summary>Initializes the tab.</summary>
		/// <param name="output">The output panel the tab belongs to.</param>
		protected OutputTabViewModel(OutputPanelViewModel output)
		{
			ArgumentNullException.ThrowIfNull(output);
			this.Output = output;
		}

		/// <summary>Gets the output panel the tab belongs to.</summary>
		public OutputPanelViewModel Output { get; }

		/// <summary>Gets which of the permanent tabs this is, or <see langword="null"/> for a closable results tab.</summary>
		public virtual OutputTab? Kind => null;
	}
}
