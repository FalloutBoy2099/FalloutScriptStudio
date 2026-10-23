namespace FalloutScriptStudio.Core.Help
{
	using System;

	/// <summary>A piece of a signature with the part it belongs to.</summary>
	/// <param name="Text">The text (a type, a name, a separator).</param>
	/// <param name="Kind">The part of the signature.</param>
	public readonly record struct SignatureSegment(String Text, SignatureSegmentKind Kind);
}