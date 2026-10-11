namespace FalloutScriptStudio.Core.Parsing.Interop
{
	/// <summary>
	/// Whether a native parse could be carried out.
	/// </summary>
	public enum NativeParseStatus
	{
		/// <summary>The native parser ran; see <see cref="NativeParseResult.ExitCode"/> for its verdict.</summary>
		Completed,

		/// <summary>The native parser cannot be used: <c>parser.dll</c> is not configured, missing or not loadable in this process.</summary>
		Unavailable,

		/// <summary>The native parser was loaded but the call or the reading of its data failed unexpectedly.</summary>
		Failed,
	}
}
