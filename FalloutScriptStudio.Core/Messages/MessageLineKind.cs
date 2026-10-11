namespace FalloutScriptStudio.Core.Messages
{
	/// <summary>
	/// The kind of a line (entry) in a <c>.msg</c> file.
	/// </summary>
	public enum MessageLineKind
	{
		/// <summary>An empty line.</summary>
		Blank = 0,

		/// <summary>A comment line (starts with <c>#</c>).</summary>
		Comment,

		/// <summary>A <c>{id}{sound}{text}</c> message.</summary>
		Message,

		/// <summary>A line that is neither a comment nor a well-formed message; it is preserved as is.</summary>
		Raw,
	}
}
