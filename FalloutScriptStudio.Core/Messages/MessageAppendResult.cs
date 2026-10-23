namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;

	/// <summary>What "add the missing lines" did to a target message file.</summary>
	/// <param name="Added">The numbers that were appended, ascending.</param>
	/// <param name="NotEncodable">The numbers that were left out because their text cannot be written in the encoding of the file (the reference is in another code page).</param>
	public sealed record MessageAppendResult(IReadOnlyList<Int32> Added, IReadOnlyList<Int32> NotEncodable);
}