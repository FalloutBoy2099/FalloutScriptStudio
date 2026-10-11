namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>
	/// Base class of the view models of the script tool windows. It lets a view model ask its window to close
	/// without referencing it.
	/// </summary>
	public abstract class ScriptToolViewModelBase : ObservableObject
	{
		/// <summary>Occurs when the view model wants its window to close. The argument tells whether the dialog was accepted.</summary>
		public event EventHandler<Boolean>? CloseRequested;

		/// <summary>Asks the window to close.</summary>
		/// <param name="accepted"><see langword="true"/> when the user confirmed the dialog.</param>
		protected void RequestClose(Boolean accepted)
		{
			this.CloseRequested?.Invoke(this, accepted);
		}
	}
}
