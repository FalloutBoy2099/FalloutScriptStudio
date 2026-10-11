namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;

	/// <summary>
	/// One request line sent by <see cref="OutOfProcessNativeParser"/> to the parser host: the script and the data of
	/// <see cref="ParserOptions"/> that the native parser needs.
	/// </summary>
	internal sealed class HostRequest
	{
		/// <summary>Gets or sets the protocol version, see <see cref="HostProtocol.Version"/>.</summary>
		public Int32 Version { get; set; } = HostProtocol.Version;

		/// <summary>Gets or sets the correlation number; the response carries the same value.</summary>
		public Int64 Id { get; set; }

		/// <summary>Gets or sets the command, one of the <see cref="HostProtocol"/> command names.</summary>
		public String Command { get; set; } = HostProtocol.ParseCommand;

		public String ScriptText { get; set; } = String.Empty;

		public String FilePath { get; set; } = String.Empty;

		public String? ParserLibraryPath { get; set; }

		public String? PreprocessorDefinitions { get; set; }

		public String? HeadersDirectory { get; set; }

		public Int32 BackwardCompatibilityMode { get; set; }

		public String? ScratchDirectory { get; set; }

		public String ScratchFileName { get; set; } = ParserOptions.DefaultScratchFileName;

		public String? NativeLogPath { get; set; }
	}
}
