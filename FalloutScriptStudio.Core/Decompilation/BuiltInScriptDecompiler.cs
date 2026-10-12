namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// <see cref="IScriptDecompiler"/> implementation written in C#: it needs no external program.
	/// </summary>
	/// <remarks>
	/// The Fallout 1 option of the request selects the opcode set; the indentation follows the editor's tab settings. A script whose
	/// calls do not have the arguments they are supposed to is retried with a lenient reading (see
	/// <see cref="ScriptDecompiler.DecompileLeniently"/>). The old decompiler option applies only to <c>int2ssl_v35.exe</c> and is ignored.
	/// </remarks>
	public sealed class BuiltInScriptDecompiler : IScriptDecompiler
	{
		private const String LineBreak = "\r\n";

		private readonly ILogger<BuiltInScriptDecompiler> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="BuiltInScriptDecompiler"/> class.
		/// </summary>
		/// <param name="logger">The logger; omitted for none.</param>
		public BuiltInScriptDecompiler(ILogger<BuiltInScriptDecompiler>? logger = null)
		{
			this._logger = logger ?? NullLogger<BuiltInScriptDecompiler>.Instance;
		}

		/// <summary>
		/// Creates the options of the decompiling engine from the compiler options of the editor.
		/// </summary>
		/// <param name="options">The compiler options.</param>
		/// <returns>The engine options.</returns>
		public static ScriptDecompilerOptions CreateEngineOptions(CompilerOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);

			return new ScriptDecompilerOptions
			{
				Game = options.DecompileForFallout1 ? ScriptGame.Fallout1 : ScriptGame.Fallout2,
				Indent = options.TabsToSpaces ? new String(' ', Math.Max(0, options.TabSize)) : "\t",
			};
		}

		/// <inheritdoc/>
		public async Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			CompilerOptions options = request.Options;
			CompilerPaths paths = new CompilerPaths(options);
			StringBuilder output = new StringBuilder();
			DecompilationResult? preparationFailure = DecompilationOutputFiles.Prepare(paths, options);
			if (preparationFailure != null)
			{
				outputProgress?.Report(preparationFailure.Output.TrimEnd());
				return preparationFailure;
			}

			Byte[] data;
			try
			{
				data = await File.ReadAllBytesAsync(request.ScriptPath, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				this._logger.ScriptNotRead(exception, request.ScriptPath);
				Report(output, outputProgress, "[Error] <none>:0: The script could not be read: " + exception.Message);
				return new DecompilationResult(false, null, output.ToString());
			}

			Report(output, outputProgress, "Decompiling " + Path.GetFileName(request.ScriptPath) + " with the built-in decompiler");
			ScriptDecompilerOptions engineOptions = CreateEngineOptions(options);

			DecompiledScript script;
			try
			{
				script = await Task.Run(() => ScriptDecompiler.DecompileLeniently(data, engineOptions), cancellationToken).ConfigureAwait(false);
			}
			catch (ScriptFormatException exception)
			{
				this._logger.ScriptNotDecompiled(exception, request.ScriptPath);
				Report(output, outputProgress, "[Error] <none>:0: " + exception.Message);
				return new DecompilationResult(false, null, output.ToString());
			}

			cancellationToken.ThrowIfCancellationRequested();
			foreach (String message in script.Messages)
			{
				Report(output, outputProgress, message);
			}

			// The text keeps the bytes of the compiled script (names and strings are single byte characters), as int2ssl did.
			String source = script.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", LineBreak, StringComparison.Ordinal);
			try
			{
				await File.WriteAllBytesAsync(paths.DecompilationFile, Encoding.Latin1.GetBytes(source), cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				DecompilationOutputFiles.Discard(paths);
				throw;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				this._logger.OutputNotWritten(exception, request.ScriptPath);
				Report(output, outputProgress, "[Error] <none>:0: The decompiled script could not be written: " + exception.Message);
				return new DecompilationResult(false, null, output.ToString());
			}

			Report(output, outputProgress, "[Done] Decompilation completed.");
			return DecompilationOutputFiles.Complete(paths, request, output.ToString());
		}

		private static void Report(StringBuilder output, IProgress<String>? progress, String line)
		{
			output.Append(line).Append(LineBreak);
			progress?.Report(line);
		}
	}
}
