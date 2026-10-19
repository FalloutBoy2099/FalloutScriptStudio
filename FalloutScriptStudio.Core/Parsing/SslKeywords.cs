namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Keywords and tokens of the SSL language used by the text based parsers.
	/// </summary>
	public static class SslKeywords
	{
		/// <summary>The <c>procedure</c> keyword including the trailing space.</summary>
		public const String Procedure = "procedure ";

		/// <summary>Length of <see cref="Procedure"/>.</summary>
		public const Int32 ProcedureLength = 10;

		/// <summary>The <c>begin</c> keyword.</summary>
		public const String Begin = "begin";

		/// <summary>The <c>end</c> keyword.</summary>
		public const String End = "end";

		/// <summary>The <c>variable</c> keyword including the trailing space.</summary>
		public const String Variable = "variable ";

		/// <summary>The <c>#include</c> directive including the trailing space.</summary>
		public const String Include = "#include ";

		/// <summary>The <c>#define</c> directive including the trailing space.</summary>
		public const String Define = "#define ";

		/// <summary>The line comment marker.</summary>
		public const String LineComment = "//";

		/// <summary>The start of a block comment.</summary>
		public const String BlockCommentStart = "/*";

		/// <summary>The end of a block comment.</summary>
		public const String BlockCommentEnd = "*/";
	}
}