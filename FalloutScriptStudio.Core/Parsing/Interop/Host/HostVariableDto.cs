namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Collections.Generic;

	/// <summary>Wire form of a <see cref="ScriptVariable"/>.</summary>
	internal sealed class HostVariableDto
	{
		public String Name { get; set; } = String.Empty;

		public Int32 Scope { get; set; }

		public String? DeclaredFile { get; set; }

		public String? FileName { get; set; }

		public Int32 DeclaredLine { get; set; } = -1;

		public Int32 ArgumentDeclaredLine { get; set; } = -1;

		public String? InitialValue { get; set; }

		public Int32 InitialValueKind { get; set; }

		public Int32 ArrayLength { get; set; }

		public List<HostReferenceDto> References { get; set; } = new List<HostReferenceDto>();
	}
}
