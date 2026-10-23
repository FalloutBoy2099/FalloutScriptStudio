namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>The texts of a comparison report, in the language of the interface (Core has no localization).</summary>
	/// <param name="Number">The heading of the number column.</param>
	/// <param name="Reference">The heading of the reference column.</param>
	/// <param name="Target">The heading of the target column.</param>
	/// <param name="Status">The heading of the status column.</param>
	/// <param name="Issues">The heading of the issues column.</param>
	/// <param name="StatusName">Gives the name of a status.</param>
	/// <param name="IssueNames">Gives the names of the issues of a row, one text.</param>
	public sealed record MessageReportLabels(
		String Number,
		String Reference,
		String Target,
		String Status,
		String Issues,
		Func<MessageComparisonStatus, String> StatusName,
		Func<MessageComparisonIssues, String> IssueNames);
}