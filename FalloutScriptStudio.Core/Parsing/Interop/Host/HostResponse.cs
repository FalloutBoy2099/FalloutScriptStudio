namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Collections.Generic;

	/// <summary>One response line sent by the parser host: the data of a <see cref="NativeParseResult"/>.</summary>
	internal sealed class HostResponse
	{
		public Int32 Version { get; set; } = HostProtocol.Version;

		public Int64 Id { get; set; }

		/// <summary>Gets or sets the name of a <see cref="NativeParseStatus"/> value.</summary>
		public String Status { get; set; } = nameof(NativeParseStatus.Failed);

		public Int32 ExitCode { get; set; } = -1;

		public String? Message { get; set; }

		public String Log { get; set; } = String.Empty;

		public List<HostProcedureDto> Procedures { get; set; } = [];

		public List<HostVariableDto> Variables { get; set; } = [];
	}
}