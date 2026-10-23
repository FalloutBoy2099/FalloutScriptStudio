namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>One line of the comparison of two message files: the message number, both texts and the verdict.</summary>
	/// <param name="Number">The message number.</param>
	/// <param name="Reference">The message of the reference file, or <see langword="null"/> when the reference does not have it.</param>
	/// <param name="Target">The message of the target file, or <see langword="null"/> when the target does not have it.</param>
	/// <param name="ReferenceLine">The 1-based physical line of the reference message, or -1.</param>
	/// <param name="TargetLine">The 1-based physical line of the target message, or -1.</param>
	/// <param name="Status">How the two messages compare.</param>
	/// <param name="Issues">The likely mistakes of the target text.</param>
	public sealed record MessageComparisonRow(
		Int32 Number,
		MessageEntry? Reference,
		MessageEntry? Target,
		Int32 ReferenceLine,
		Int32 TargetLine,
		MessageComparisonStatus Status,
		MessageComparisonIssues Issues)
	{
		/// <summary>Gets the text of the reference message, or an empty text.</summary>
		public String ReferenceText => this.Reference?.Text ?? String.Empty;

		/// <summary>Gets the text of the target message, or an empty text.</summary>
		public String TargetText => this.Target?.Text ?? String.Empty;

		/// <summary>Gets a value indicating whether the row is a problem to look at: any status but <see cref="MessageComparisonStatus.Ok"/>, or any issue.</summary>
		public Boolean NeedsAttention => this.Status != MessageComparisonStatus.Ok || this.Issues != MessageComparisonIssues.None;
	}
}