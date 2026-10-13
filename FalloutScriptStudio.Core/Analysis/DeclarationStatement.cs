namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>A <c>variable a, b := 1;</c> declaration: one or several variables introduced by one <c>variable</c> keyword.</summary>
	public sealed class DeclarationStatement
	{
		private readonly List<VariableDeclaration> _items = new List<VariableDeclaration>();

		/// <summary>Initializes a new instance of the <see cref="DeclarationStatement"/> class.</summary>
		/// <param name="offset">The offset of the first token (a modifier such as <c>export</c>, or the <c>variable</c> keyword).</param>
		/// <param name="inParentheses">Whether the statement is inside parentheses.</param>
		public DeclarationStatement(Int32 offset, Boolean inParentheses)
		{
			this.Offset = offset;
			this.IsInParentheses = inParentheses;
		}

		/// <summary>Gets the offset of the first token of the statement.</summary>
		public Int32 Offset { get; }

		/// <summary>Gets the offset just after the statement (after the <c>;</c> when it has one).</summary>
		public Int32 End { get; internal set; }

		/// <summary>Gets a value indicating whether the statement ends with a semicolon.</summary>
		public Boolean IsTerminated { get; internal set; }

		/// <summary>Gets a value indicating whether the statement is inside parentheses (a parameter list or the header of a loop).</summary>
		public Boolean IsInParentheses { get; }

		/// <summary>Gets the variables of the statement, in order.</summary>
		public IReadOnlyList<VariableDeclaration> Items => this._items;

		internal void Add(VariableDeclaration item)
		{
			item.ItemIndex = this._items.Count;
			item.Statement = this;
			this._items.Add(item);
		}
	}
}
