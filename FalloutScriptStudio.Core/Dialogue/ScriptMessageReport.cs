namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The result of the check of a script against its message file.</summary>
	/// <param name="Findings">The findings: missing numbers in script order, then the problems of the message file in file order.</param>
	/// <param name="MissingNumbers">The distinct numbers of the message file of the script that the script uses and the file lacks, ascending (what "add the missing messages" appends).</param>
	/// <param name="UsedNumbers">The distinct numbers of the message file of the script that the script uses, ascending.</param>
	/// <param name="Scan">What was found in the script.</param>
	public sealed record ScriptMessageReport(
		IReadOnlyList<ScriptMessageFinding> Findings,
		IReadOnlyList<Int32> MissingNumbers,
		IReadOnlyList<Int32> UsedNumbers,
		ScriptMessageScan Scan)
	{
		/// <summary>Gets the findings of one kind.</summary>
		/// <param name="kind">The kind.</param>
		/// <returns>The findings.</returns>
		public IEnumerable<ScriptMessageFinding> Of(ScriptMessageFindingKind kind)
		{
			return this.Findings.Where(finding => finding.Kind == kind);
		}
	}
}