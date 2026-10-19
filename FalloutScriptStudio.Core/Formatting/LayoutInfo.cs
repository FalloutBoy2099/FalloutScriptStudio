namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;

	/// <summary>What the layout analysis found out about a text: where each line should start and how the statements are laid out.</summary>
	internal sealed class LayoutInfo
	{
		/// <summary>Initializes a new instance of the <see cref="LayoutInfo"/> class.</summary>
		/// <param name="lineCount">The number of lines.</param>
		/// <param name="tokenCount">The number of tokens.</param>
		public LayoutInfo(Int32 lineCount, Int32 tokenCount)
		{
			this.Indents = new String?[lineCount];
			this.IsContinuation = new Boolean[lineCount];
			this.StatementLine = new Int32[lineCount];
			this.DepthBefore = new Int32[tokenCount];
		}

		/// <summary>Gets the leading white space each line should have; <see langword="null"/> for a line that is left alone (a blank line, a preprocessor line, the inside of a comment).</summary>
		public String?[] Indents { get; }

		/// <summary>Gets a value for each line: it continues a statement that began on an earlier line.</summary>
		public Boolean[] IsContinuation { get; }

		/// <summary>Gets, for each line, the line on which the statement it belongs to began (its own number for a line that starts a statement).</summary>
		public Int32[] StatementLine { get; }

		/// <summary>Gets, for each token, how many brackets are open before it.</summary>
		public Int32[] DepthBefore { get; }

		/// <summary>Gets the lines on which a procedure definition (a procedure with a body) starts.</summary>
		public HashSet<Int32> ProcedureStartLines { get; } = [];

		/// <summary>Gets the lines on which the body of a procedure ends.</summary>
		public HashSet<Int32> ProcedureEndLines { get; } = [];
	}
}