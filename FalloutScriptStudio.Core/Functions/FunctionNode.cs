namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A node of the functions tree: a category, a function or a separator. Instances are immutable snapshots; after the
	/// ini file is changed the tree has to be loaded again.
	/// </summary>
	public sealed class FunctionNode
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FunctionNode"/> class.
		/// </summary>
		/// <param name="kind">The kind of the node.</param>
		/// <param name="name">The category title or function name.</param>
		/// <param name="source">The file the node was read from.</param>
		public FunctionNode(FunctionNodeKind kind, String name, FunctionSource source)
		{
			ArgumentNullException.ThrowIfNull(name);
			this.Kind = kind;
			this.Name = name;
			this.Source = source;
		}

		/// <summary>Gets the kind of the node.</summary>
		public FunctionNodeKind Kind { get; }

		/// <summary>Gets the category title or the function name (the separator uses 50 dashes like the legacy tree).</summary>
		public String Name { get; }

		/// <summary>Gets the file the node was read from.</summary>
		public FunctionSource Source { get; }

		/// <summary>Gets a value indicating whether the node comes from the user's file (and can be edited freely).</summary>
		public Boolean IsUser => this.Source == FunctionSource.User;

		/// <summary>Gets the short description of a function (the tool tip); empty for the other kinds.</summary>
		public String Description { get; init; } = String.Empty;

		/// <summary>
		/// Gets the code of a function as stored in the file, where <c>&lt;cr&gt;</c> stands for a line break;
		/// empty for the other kinds. Use <see cref="FunctionInsertion"/> to turn it into text for the editor.
		/// </summary>
		public String Code { get; init; } = String.Empty;

		/// <summary>Gets the number of ancestors: 0 for a top level category, 1 for its functions and sub categories.</summary>
		public Int32 Depth { get; init; }

		/// <summary>Gets the children of a category in file order (empty for the other kinds).</summary>
		public IReadOnlyList<FunctionNode> Children { get; init; } = Array.Empty<FunctionNode>();

		/// <summary>Gets the zero-based index of the line of the node in its file (the <c>&lt;m&gt;</c> line of a category).</summary>
		public Int32 SourceLine { get; init; } = -1;

		/// <summary>
		/// Gets the zero-based index of the last line that belongs to the node (the closing <c>&lt;m-&gt;</c> of a sub category);
		/// equal to <see cref="SourceLine"/> for functions.
		/// </summary>
		public Int32 LastLine { get; init; } = -1;

		/// <summary>Gets the trimmed text of the line of the node in the file; used to detect that the file changed.</summary>
		public String RawLine { get; init; } = String.Empty;

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Name;
		}
	}
}
