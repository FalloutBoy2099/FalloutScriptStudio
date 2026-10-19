namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The previously used search texts and the folders offered by the search in files dialog.
	/// </summary>
	public sealed class SearchHistory
	{
		/// <summary>The maximum number of remembered search texts.</summary>
		public const Int32 MaxQueries = 150;

		/// <summary>Gets or sets the search texts, most recent first.</summary>
		public List<String> Queries { get; set; } = [];

		/// <summary>Gets or sets the folders used for searches in files.</summary>
		public List<String> Directories { get; set; } = [];

		/// <summary>
		/// Moves a search text to the top of the history.
		/// </summary>
		/// <param name="query">The search text.</param>
		public void AddQuery(String query)
		{
			if (String.IsNullOrEmpty(query))
			{
				return;
			}

			this.Queries.RemoveAll(item => String.Equals(item, query, StringComparison.Ordinal));
			this.Queries.Insert(0, query);
			if (this.Queries.Count > MaxQueries)
			{
				this.Queries.RemoveRange(MaxQueries, this.Queries.Count - MaxQueries);
			}
		}
	}
}