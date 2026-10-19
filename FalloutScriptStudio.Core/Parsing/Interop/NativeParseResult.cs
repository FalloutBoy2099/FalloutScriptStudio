namespace FalloutScriptStudio.Core.Parsing.Interop
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Result of a native parse, expressed in the public domain model.
	/// </summary>
	/// <param name="Status">Whether the native parser ran.</param>
	public sealed record NativeParseResult(NativeParseStatus Status)
	{
		/// <summary>
		/// Gets the status code returned by <c>parse_main</c> when <see cref="Status"/> is <see cref="NativeParseStatus.Completed"/>:
		/// 0 = the script is valid, -1 = semantic errors (the procedures and variables are still available), 1 = syntax error,
		/// 2 = preprocessor error (for example a missing include file; no data).
		/// </summary>
		public Int32 ExitCode { get; init; } = -1;

		/// <summary>Gets the procedures; filled only when <see cref="HasData"/> is <c>true</c>.</summary>
		public IReadOnlyList<ScriptProcedure> Procedures { get; init; } = Array.Empty<ScriptProcedure>();

		/// <summary>Gets the script-level variables; filled only when <see cref="HasData"/> is <c>true</c>.</summary>
		public IReadOnlyList<ScriptVariable> Variables { get; init; } = Array.Empty<ScriptVariable>();

		/// <summary>Gets the raw text the native parser wrote to its log file; empty when there is none.</summary>
		public String Log { get; init; } = String.Empty;

		/// <summary>Gets an explanation for <see cref="NativeParseStatus.Unavailable"/> and <see cref="NativeParseStatus.Failed"/>.</summary>
		public String? Message { get; init; }

		/// <summary>Gets a value indicating whether the native parser ran and accepted the script.</summary>
		public Boolean Succeeded => this.Status == NativeParseStatus.Completed && this.ExitCode == 0;

		/// <summary>
		/// Gets a value indicating whether <see cref="Procedures"/> and <see cref="Variables"/> were read: the script is valid or only has
		/// semantic errors (status <c>0</c> or <c>-1</c>; the legacy editor read the data whenever the status was not positive).
		/// </summary>
		public Boolean HasData => this.Status == NativeParseStatus.Completed && this.ExitCode <= 0;
	}
}