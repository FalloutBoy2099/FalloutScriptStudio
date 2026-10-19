namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// Options of <see cref="IFlowchartBuilder"/>.
	/// </summary>
	public sealed record FlowchartBuildOptions
	{
		/// <summary>Gets a value indicating whether the exit node <c>Node999</c> is left out (legacy setting "show exit node" turned off).</summary>
		public Boolean ExcludeExitNode { get; init; }

		/// <summary>Gets a value indicating whether commented code is shown in the nodes.</summary>
		public Boolean ShowCommentedCode { get; init; }

		/// <summary>Gets the number of spaces removed from the start of every shown line (one indentation level).</summary>
		public Int32 TabSize { get; init; } = 4;
	}
}