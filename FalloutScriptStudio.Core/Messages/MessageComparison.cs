namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The result of <see cref="MessageFileComparer"/>: a row per message number of either file, ascending, and the notes about the files.</summary>
	public sealed class MessageComparison
	{
		private readonly Int32[] _counts;

		/// <summary>Initializes a new instance of the <see cref="MessageComparison"/> class.</summary>
		/// <param name="rows">The rows, ascending by number.</param>
		/// <param name="notes">The notes about the files.</param>
		public MessageComparison(IReadOnlyList<MessageComparisonRow> rows, IReadOnlyList<MessageComparisonNote> notes)
		{
			ArgumentNullException.ThrowIfNull(rows);
			ArgumentNullException.ThrowIfNull(notes);
			this.Rows = rows;
			this.Notes = notes;
			this._counts = new Int32[Enum.GetValues<MessageComparisonStatus>().Length];
			foreach (MessageComparisonRow row in rows)
			{
				this._counts[(Int32)row.Status]++;
				if (row.Issues != MessageComparisonIssues.None)
				{
					this.IssueCount++;
				}
			}
		}

		/// <summary>Gets an empty comparison.</summary>
		public static MessageComparison Empty { get; } = new MessageComparison(Array.Empty<MessageComparisonRow>(), Array.Empty<MessageComparisonNote>());

		/// <summary>Gets the rows, ascending by message number.</summary>
		public IReadOnlyList<MessageComparisonRow> Rows { get; }

		/// <summary>Gets the notes about the files.</summary>
		public IReadOnlyList<MessageComparisonNote> Notes { get; }

		/// <summary>Gets the number of rows that have at least one issue.</summary>
		public Int32 IssueCount { get; }

		/// <summary>Gets the number of rows with a status.</summary>
		/// <param name="status">The status.</param>
		/// <returns>The count.</returns>
		public Int32 Count(MessageComparisonStatus status)
		{
			return this._counts[(Int32)status];
		}

		/// <summary>Gets the numbers of the messages the target lacks, ascending.</summary>
		/// <returns>The numbers.</returns>
		public IReadOnlyList<Int32> GetMissingNumbers()
		{
			return this.Rows.Where(row => row.Status == MessageComparisonStatus.MissingInTarget).Select(row => row.Number).ToList();
		}
	}
}