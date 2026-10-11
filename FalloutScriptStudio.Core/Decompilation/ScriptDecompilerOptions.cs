namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>How a script is decompiled.</summary>
	public sealed class ScriptDecompilerOptions
	{
		/// <summary>The game the script is for (the opcodes that only one game has are named accordingly).</summary>
		public ScriptGame Game { get; set; } = ScriptGame.Fallout2;

		/// <summary>
		/// Goes on when a call has fewer arguments than its function takes (some compilers leave out the last ones) instead of
		/// failing.
		/// </summary>
		public Boolean IgnoreWrongArgumentCount { get; set; }

		/// <summary>When arguments are missing, supposes they are the ones before the call, not the ones after.</summary>
		public Boolean InsertOmittedArgumentsBackward { get; set; }

		/// <summary>Writes <c>and</c> and <c>or</c> as the operators that always evaluate both sides, as the old compilers did.</summary>
		public Boolean UseOldShortCircuitKeywords { get; set; }

		/// <summary>What one level of indentation is made of.</summary>
		public String Indent { get; set; } = "\t";

		internal ScriptDecompilerOptions Clone() => (ScriptDecompilerOptions)MemberwiseClone();
	}
}
