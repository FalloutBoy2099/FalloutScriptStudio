namespace FalloutScriptStudio.Core.Refactoring
{
	using System;

	/// <summary>
	/// Result of checking a new name before renaming.
	/// </summary>
	/// <param name="Error">The problem, or <see cref="RenameError.None"/>.</param>
	/// <param name="NewName">The cleaned-up name (trimmed).</param>
	/// <param name="Message">A description of the problem, or <c>null</c>.</param>
	public sealed record RenameValidation(RenameError Error, String NewName, String? Message)
	{
		/// <summary>Gets a value indicating whether the name can be used.</summary>
		public Boolean IsValid => this.Error == RenameError.None;
	}
}