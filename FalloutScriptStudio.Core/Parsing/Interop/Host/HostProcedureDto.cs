namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Collections.Generic;

	/// <summary>Wire form of a <see cref="ScriptProcedure"/>.</summary>
	internal sealed class HostProcedureDto
	{
		public String Name { get; set; } = String.Empty;

		public Int32 Flags { get; set; }

		public String DeclaredFile { get; set; } = String.Empty;

		public String StartFile { get; set; } = String.Empty;

		public String FileName { get; set; } = String.Empty;

		public Int32 DeclaredLine { get; set; } = -1;

		public Int32 DefinedLine { get; set; } = -1;

		public Int32 StartLine { get; set; } = -1;

		public Int32 EndLine { get; set; } = -1;

		public Int32 ArgumentCount { get; set; }

		public Int32 TimeValue { get; set; }

		public List<HostVariableDto> Variables { get; set; } = new List<HostVariableDto>();

		public List<HostReferenceDto> References { get; set; } = new List<HostReferenceDto>();
	}
}
