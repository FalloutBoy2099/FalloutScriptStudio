namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Base class of the view models of the tool windows. It lets a view model ask its window to close
	/// without referencing it.
	/// </summary>
	public abstract class ToolWindowViewModelBase : ObservableObject
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ToolWindowViewModelBase"/> class.
		/// </summary>
		protected ToolWindowViewModelBase()
		{
			_ = LanguageChangedSubscription.Subscribe(this, static viewModel => viewModel.OnLanguageChanged());
		}

		/// <summary>
		/// Occurs when the view model wants its window to close. The argument tells whether the dialog was accepted.
		/// </summary>
		public event EventHandler<Boolean>? CloseRequested;

		/// <summary>
		/// Called after the language of the interface changed; a view model that keeps texts of its own refreshes them here.
		/// </summary>
		protected virtual void OnLanguageChanged()
		{
		}

		/// <summary>
		/// Asks the window to close.
		/// </summary>
		/// <param name="accepted"><see langword="true"/> when the user confirmed the dialog.</param>
		protected void RequestClose(Boolean accepted)
		{
			this.CloseRequested?.Invoke(this, accepted);
		}
	}
}