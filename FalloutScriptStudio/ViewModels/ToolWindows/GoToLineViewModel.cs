namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Globalization;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// View model of the "Go to line" prompt.
	/// </summary>
	public sealed partial class GoToLineViewModel : ToolWindowViewModelBase
	{
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(ParsedLine))]
		[NotifyPropertyChangedFor(nameof(IsValid))]
		[NotifyCanExecuteChangedFor(nameof(GoCommand))]
		private String _lineText = "1";

		private Int32 _lineCount = 1;

		/// <summary>Gets the number of lines of the document (the largest valid line number).</summary>
		public Int32 LineCount => this._lineCount;

		/// <summary>Gets the caption of the input field.</summary>
		public String Prompt => Loc.Format("Str.ToolWindows.GoToLine.Prompt", this._lineCount);

		/// <summary>
		/// Gets the entered line, limited to the range of the document, or <see langword="null"/> when the text is not a number.
		/// </summary>
		public Int32? ParsedLine => ParseLine(this.LineText, this._lineCount);

		/// <summary>Gets a value indicating whether the entered text is a line number.</summary>
		public Boolean IsValid => this.ParsedLine.HasValue;

		/// <summary>Gets the line the user chose, or <see langword="null"/> when the prompt was cancelled.</summary>
		public Int32? SelectedLine { get; private set; }

		/// <summary>
		/// Prepares the prompt.
		/// </summary>
		/// <param name="currentLine">The line the caret is on (1-based).</param>
		/// <param name="lineCount">The number of lines of the document.</param>
		public void Initialize(Int32 currentLine, Int32 lineCount)
		{
			this._lineCount = Math.Max(1, lineCount);
			this.OnPropertyChanged(nameof(this.LineCount));
			this.OnPropertyChanged(nameof(this.Prompt));
			this.LineText = Math.Clamp(currentLine, 1, this._lineCount).ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Parses the text of the input field.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="lineCount">The number of lines of the document.</param>
		/// <returns>The line number limited to <c>1..lineCount</c>, or <see langword="null"/> when the text is not a number.</returns>
		public static Int32? ParseLine(String? text, Int32 lineCount)
		{
			String trimmed = (text ?? String.Empty).Trim();
			if (trimmed.Length == 0)
			{
				return null;
			}

			foreach (Char c in trimmed)
			{
				if (c < '0' || c > '9')
				{
					return null;
				}
			}

			Int32 maximum = Math.Max(1, lineCount);
			if (!Int32.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out Int32 value))
			{
				return maximum;
			}

			return Math.Clamp(value, 1, maximum);
		}

		[RelayCommand(CanExecute = nameof(IsValid))]
		private void Go()
		{
			this.SelectedLine = this.ParsedLine;
			this.RequestClose(true);
		}

		[RelayCommand]
		private void Cancel()
		{
			this.SelectedLine = null;
			this.RequestClose(false);
		}
	}
}