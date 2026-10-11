namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// The result of a message file structure check.
	/// </summary>
	/// <param name="Issues">The problems in the order number errors, bracket error, style hints, missing comment marks.</param>
	public sealed record MessageStructureReport(IReadOnlyList<MessageStructureIssue> Issues)
	{
		/// <summary>Gets a value indicating whether at least one issue is an error.</summary>
		public Boolean HasErrors => this.Issues.Any(issue => issue.Type == BuildErrorType.Error);

		/// <summary>
		/// Converts the issues to build log entries for the error list.
		/// </summary>
		/// <param name="fileName">The file the text belongs to.</param>
		/// <returns>The entries.</returns>
		public IReadOnlyList<BuildError> ToBuildErrors(String fileName)
		{
			return this.Issues
				.Select(issue => new BuildError(issue.Type, issue.Message, fileName, issue.Line, issue.Column, issue.Length))
				.ToList();
		}
	}
}
