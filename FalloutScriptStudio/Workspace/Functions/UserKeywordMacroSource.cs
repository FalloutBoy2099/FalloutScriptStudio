namespace FalloutScriptStudio.Workspace.Functions
{
	using System;
	using System.Collections.Generic;
	using Core.Functions;
	using Editor;

	/// <summary>
	/// Tells the editor which user keywords (the functions the user chose to highlight, for example <c>DEBUGMSG</c>)
	/// it should highlight like macros.
	/// </summary>
	public sealed class UserKeywordMacroSource : IUserMacroSource
	{
		private readonly IUserKeywordStore _keywords;

		/// <summary>Initializes the source.</summary>
		public UserKeywordMacroSource(IUserKeywordStore keywords)
		{
			ArgumentNullException.ThrowIfNull(keywords);
			this._keywords = keywords;
			this._keywords.Changed += (_, _) => this.Changed?.Invoke(this, EventArgs.Empty);
		}

		public event EventHandler? Changed;

		public IReadOnlyCollection<String> GetWords()
		{
			return this._keywords.Keywords;
		}
	}
}