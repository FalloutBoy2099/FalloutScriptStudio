namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// <see cref="IScriptDecompiler"/> that hands each request to the decompiler its options choose
	/// (<see cref="CompilerOptions.Decompiler"/>).
	/// </summary>
	public sealed class ScriptDecompilerSelector : IScriptDecompiler
	{
		private readonly IScriptDecompiler _builtIn;
		private readonly IScriptDecompiler _external;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptDecompilerSelector"/> class.
		/// </summary>
		/// <param name="builtIn">The decompiler used for <see cref="DecompilerKind.BuiltIn"/>.</param>
		/// <param name="external">The decompiler used for <see cref="DecompilerKind.ExternalInt2Ssl"/>.</param>
		public ScriptDecompilerSelector(IScriptDecompiler builtIn, IScriptDecompiler external)
		{
			ArgumentNullException.ThrowIfNull(builtIn);
			ArgumentNullException.ThrowIfNull(external);
			this._builtIn = builtIn;
			this._external = external;
		}

		/// <inheritdoc/>
		public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			IScriptDecompiler decompiler = request.Options.Decompiler == DecompilerKind.ExternalInt2Ssl ? this._external : this._builtIn;
			return decompiler.DecompileAsync(request, outputProgress, cancellationToken);
		}
	}
}
