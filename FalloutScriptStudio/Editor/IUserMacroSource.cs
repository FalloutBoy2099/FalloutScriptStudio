namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Optional service that supplies the user defined macro words (for example from the user function list) which the
	/// editor highlights with the "user macros" color. Register an implementation to enable the feature.
	/// </summary>
	public interface IUserMacroSource
	{
		/// <summary>Raised when the list of words changed; open editors re-apply their highlighting.</summary>
		event EventHandler? Changed;

		/// <summary>Gets the current words.</summary>
		IReadOnlyCollection<String> GetWords();
	}
}