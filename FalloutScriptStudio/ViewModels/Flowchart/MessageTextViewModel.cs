namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.ViewModels.ToolWindows;

	/// <summary>
	/// The view model of the dialog that edits the text of one message (the quick message edit of the flowchart).
	/// </summary>
	public sealed partial class MessageTextViewModel : ToolWindowViewModelBase
	{
		/// <summary>Gets or sets the text above the edit box.</summary>
		[ObservableProperty]
		public partial String Caption { get; set; } = String.Empty;

		/// <summary>Gets or sets the text of the message.</summary>
		[ObservableProperty]
		public partial String Text { get; set; } = String.Empty;

		/// <summary>Gets the text the user confirmed, or <see langword="null"/> when the dialog was cancelled.</summary>
		public String? Result { get; private set; }

		/// <summary>Sets what the dialog shows.</summary>
		/// <param name="caption">The text above the edit box.</param>
		/// <param name="text">The current text of the message.</param>
		public void Initialize(String caption, String text)
		{
			ArgumentNullException.ThrowIfNull(caption);
			ArgumentNullException.ThrowIfNull(text);
			this.Caption = caption;
			this.Text = text;
			this.Result = null;
		}

		/// <summary>Accepts the text.</summary>
		[RelayCommand]
		public void Accept()
		{
			this.Result = this.Text;
			this.RequestClose(true);
		}

		/// <summary>Closes the dialog without a result.</summary>
		[RelayCommand]
		public void Cancel()
		{
			this.RequestClose(false);
		}
	}
}
