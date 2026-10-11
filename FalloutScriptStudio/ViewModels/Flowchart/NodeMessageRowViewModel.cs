namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.IO;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Dialogue;

	/// <summary>
	/// A row of the message grid of the node code editor: a message used by the code of the node.
	/// </summary>
	public sealed partial class NodeMessageRowViewModel : ObservableObject
	{
		private readonly Action<NodeMessageRowViewModel>? _edited;
		private Boolean _initializing = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeMessageRowViewModel"/> class.
		/// </summary>
		/// <param name="source">The message the code uses.</param>
		/// <param name="edited">Called after the user changed the text of the message.</param>
		public NodeMessageRowViewModel(NodeMessageRow source, Action<NodeMessageRowViewModel>? edited = null)
		{
			ArgumentNullException.ThrowIfNull(source);
			this.Source = source;
			this._edited = edited;
			this.Text = source.Text;
			this._initializing = false;
		}

		/// <summary>Gets the message with its statement.</summary>
		public NodeMessageRow Source { get; }

		/// <summary>Gets the message number.</summary>
		public Int32 MessageNumber => this.Source.MessageNumber;

		/// <summary>Gets the path of the message file, or <see langword="null"/> when it is missing.</summary>
		public String? FilePath => this.Source.MessageFilePath;

		/// <summary>Gets the name of the message file.</summary>
		public String FileName => this.FilePath is null ? String.Empty : Path.GetFileName(this.FilePath);

		/// <summary>Gets a value indicating whether the message is a player option.</summary>
		public Boolean IsOption => this.Source.IsOption;

		/// <summary>Gets a value indicating whether the message could not be read; its text cannot be edited.</summary>
		public Boolean IsError => this.Source.IsError;

		/// <summary>Gets a value indicating whether the text can be edited.</summary>
		public Boolean CanEditText => !this.Source.IsError && this.FilePath is not null;

		/// <summary>Gets a value indicating whether the text is protected against editing.</summary>
		public Boolean IsReadOnlyText => !this.CanEditText;

		/// <summary>Gets or sets the text of the message; changing it changes the message file.</summary>
		[ObservableProperty]
		public partial String Text { get; set; } = String.Empty;

		partial void OnTextChanged(String value)
		{
			if (!this._initializing && this.CanEditText)
			{
				this._edited?.Invoke(this);
			}
		}
	}
}
