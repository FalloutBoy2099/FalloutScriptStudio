namespace FalloutScriptStudio.Core.Refactoring
{
	/// <summary>Why a block of statements cannot be extracted into a procedure.</summary>
	public enum ExtractFailure
	{
		/// <summary>Nothing is wrong: the block can be extracted.</summary>
		None = 0,

		/// <summary>The selection holds no statement.</summary>
		EmptySelection = 1,

		/// <summary>The selection is not inside the body of a procedure.</summary>
		NotInsideProcedure = 2,

		/// <summary>The selection contains a preprocessor line.</summary>
		ContainsPreprocessorLine = 3,

		/// <summary>The <c>begin</c> and <c>end</c> keywords, the parentheses or the brackets of the selection do not match.</summary>
		UnbalancedBlock = 4,

		/// <summary>The selection starts or ends inside a statement, or splits an <c>if ... else</c>.</summary>
		PartialStatement = 5,

		/// <summary>The selection contains <c>return</c>, which would only leave the new procedure.</summary>
		ContainsReturn = 6,

		/// <summary>The selection contains <c>break</c> or <c>continue</c> that belongs to a loop outside the selection.</summary>
		ContainsLoopControl = 7,

		/// <summary>A variable declared inside the selection is used after it.</summary>
		DeclaredVariableUsedAfter = 8,

		/// <summary>The selection changes several variables that are used after it, and a procedure returns one value.</summary>
		TooManyResults = 9,
	}
}