namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;

	/// <summary>Wire form of a <see cref="SourceReference"/>.</summary>
	internal sealed class HostReferenceDto
	{
		public String? File { get; set; }

		public Int32 Line { get; set; }
	}
}
