namespace FalloutScriptStudio.Core.Building
{
	using System;

	/// <summary>One <c>#include</c> directive of a source file.</summary>
	/// <param name="Path">The path as written between the quotes (or the angle brackets), or <see langword="null"/> when the directive does not name a file literally (<c>#include SOME_MACRO</c>).</param>
	/// <param name="Line">The 1-based line of the directive.</param>
	/// <param name="Conditional">Whether the directive is inside an <c>#if</c>, <c>#ifdef</c> or <c>#ifndef</c> block, so that the compiler may skip it.</param>
	public sealed record IncludeDirective(String? Path, Int32 Line, Boolean Conditional)
	{
		/// <summary>Gets a value indicating whether the directive names its file literally.</summary>
		public Boolean IsLiteral => this.Path is { Length: > 0 };
	}
}