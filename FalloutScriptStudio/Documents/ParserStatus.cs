namespace FalloutScriptStudio.Documents
{
	using System;
	using System.Collections.Generic;
	using Localization;

	/// <summary>
	/// The status of the parser of one document: a kind and the arguments of its message. The text is only computed when it is
	/// asked for, so it always follows the language in use at that moment.
	/// </summary>
	/// <param name="Kind">The state of the parser.</param>
	/// <param name="Arguments">The arguments of the message (none for the states that have no placeholder).</param>
	public sealed record ParserStatus(ParserStatusKind Kind, IReadOnlyList<Object?> Arguments)
	{
		/// <summary>Gets the status of a document the parser has not been asked about.</summary>
		public static readonly ParserStatus None = new(ParserStatusKind.None);

		/// <summary>Initializes a new instance of the <see cref="ParserStatus"/> record for a state whose message has no placeholder.</summary>
		/// <param name="kind">The state of the parser.</param>
		public ParserStatus(ParserStatusKind kind)
			: this(kind, Array.Empty<Object?>())
		{
		}

		/// <summary>Gets the text of the status in the language in use now; empty for <see cref="ParserStatusKind.None"/>.</summary>
		/// <returns>The localized text.</returns>
		public String ToText()
		{
			String? key = this.Kind switch
			{
				ParserStatusKind.Waiting => "Str.Shell.Parser.Waiting",
				ParserStatusKind.OffMacros => "Str.Shell.Parser.OffMacros",
				ParserStatusKind.Running => "Str.Shell.Parser.Running",
				ParserStatusKind.FailedLog => "Str.Shell.Parser.FailedLog",
				ParserStatusKind.Builtin => "Str.Shell.Parser.Builtin",
				ParserStatusKind.FailedParserLog => "Str.Shell.Parser.FailedParserLog",
				ParserStatusKind.SyntaxError => "Str.Shell.Parser.SyntaxError",
				ParserStatusKind.Done => "Str.Shell.Parser.Done",
				_ => null,
			};

			if (key is null)
			{
				return String.Empty;
			}

			return this.Arguments.Count == 0 ? Loc.Get(key) : Loc.Format(key, this.Arguments is Object?[] array ? array : new List<Object?>(this.Arguments).ToArray());
		}
	}
}