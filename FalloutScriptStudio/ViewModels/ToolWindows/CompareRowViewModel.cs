namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Globalization;
	using Core.Messages;

	/// <summary>One line of the table of the comparison of message files. The texts that depend on the language are computed when they are read.</summary>
	public sealed class CompareRowViewModel
	{
		/// <summary>Initializes a new instance of the <see cref="CompareRowViewModel"/> class.</summary>
		/// <param name="row">The row of the comparison.</param>
		public CompareRowViewModel(MessageComparisonRow row)
		{
			ArgumentNullException.ThrowIfNull(row);
			this.Row = row;
		}

		/// <summary>Gets the row of the comparison.</summary>
		public MessageComparisonRow Row { get; }

		/// <summary>Gets the message number.</summary>
		public Int32 Number => this.Row.Number;

		/// <summary>Gets the reference text on one line (the line breaks of the message are shown as a symbol).</summary>
		public String ReferenceText => OneLine(this.Row.ReferenceText);

		/// <summary>Gets the target text on one line.</summary>
		public String TargetText => OneLine(this.Row.TargetText);

		/// <summary>Gets the status.</summary>
		public MessageComparisonStatus Status => this.Row.Status;

		/// <summary>Gets the status as a word, in the language of the interface.</summary>
		public String StatusText => ComparisonTexts.StatusName(this.Row.Status);

		/// <summary>Gets the likely mistakes of the target text, in the language of the interface.</summary>
		public String IssuesText => ComparisonTexts.IssueNames(this.Row.Issues);

		/// <summary>Gets the name a screen reader reads for the row.</summary>
		public String AccessibleName
		{
			get
			{
				String issues = this.IssuesText;
				return this.Row.Number.ToString(CultureInfo.InvariantCulture) + ", " + this.StatusText + (issues.Length > 0 ? ", " + issues : String.Empty) + ": " + this.TargetText;
			}
		}

		private static String OneLine(String text)
		{
			return text.IndexOf('\n', StringComparison.Ordinal) < 0 ? text : text.Replace("\r\n", " ↵ ", StringComparison.Ordinal).Replace("\n", " ↵ ", StringComparison.Ordinal);
		}
	}
}