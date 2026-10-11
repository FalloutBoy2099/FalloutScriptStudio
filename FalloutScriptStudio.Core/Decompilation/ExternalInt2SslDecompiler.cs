namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// <see cref="IScriptDecompiler"/> implementation that runs an external <c>int2ssl.exe</c>.
	/// </summary>
	/// <remarks>
	/// The program is <see cref="CompilerOptions.ExternalDecompilerPath"/> or, when it is not set, <c>int2ssl.exe</c> in the resources
	/// folder. When it fails, <c>int2ssl_v35.exe</c> of the same folder is tried; with the old decompiler option only that one is run.
	/// </remarks>
	public sealed class ExternalInt2SslDecompiler : IScriptDecompiler
	{
		/// <summary>The name of the current decompiler.</summary>
		public const String DefaultProgramName = "int2ssl.exe";

		/// <summary>The name of the old decompiler, the fallback of the current one.</summary>
		public const String LegacyProgramName = "int2ssl_v35.exe";

		private const String LineBreak = "\r\n";

		private readonly IProcessRunner _processRunner;
		private readonly ILogger<ExternalInt2SslDecompiler> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="ExternalInt2SslDecompiler"/> class.
		/// </summary>
		/// <param name="processRunner">Starts the external tool.</param>
		/// <param name="logger">The logger; omitted for none.</param>
		public ExternalInt2SslDecompiler(IProcessRunner processRunner, ILogger<ExternalInt2SslDecompiler>? logger = null)
		{
			ArgumentNullException.ThrowIfNull(processRunner);
			this._processRunner = processRunner;
			this._logger = logger ?? NullLogger<ExternalInt2SslDecompiler>.Instance;
		}

		/// <summary>
		/// Gets the full paths of the programs that are tried, in order.
		/// </summary>
		/// <param name="options">The compiler options.</param>
		/// <returns>One path, or two when the old decompiler is the fallback.</returns>
		public static IReadOnlyList<String> GetPrograms(CompilerOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);

			String directory = options.ResourcesDirectory;
			String primary = DefaultProgramName;
			String? configured = options.ExternalDecompilerPath;
			if (!String.IsNullOrWhiteSpace(configured))
			{
				if (Directory.Exists(configured))
				{
					directory = configured;
				}
				else
				{
					directory = Path.GetDirectoryName(configured) is { Length: > 0 } configuredDirectory ? configuredDirectory : directory;
					primary = Path.GetFileName(configured);
				}
			}

			List<String> programs = new List<String>();
			if (!options.UseLegacyDecompiler)
			{
				programs.Add(Path.Combine(directory, primary));
			}

			if (options.UseLegacyDecompiler || !String.Equals(primary, LegacyProgramName, StringComparison.OrdinalIgnoreCase))
			{
				programs.Add(Path.Combine(directory, LegacyProgramName));
			}

			return programs;
		}

		/// <inheritdoc/>
		public async Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			CompilerOptions options = request.Options;
			CompilerPaths paths = new CompilerPaths(options);
			CompilerCommandLineBuilder commandLines = new CompilerCommandLineBuilder(options);
			StringBuilder output = new StringBuilder();
			DecompilationResult? preparationFailure = DecompilationOutputFiles.Prepare(paths, options);
			if (preparationFailure != null)
			{
				outputProgress?.Report(preparationFailure.Output.TrimEnd());
				return preparationFailure;
			}

			Boolean succeeded = false;
			foreach (String exePath in GetPrograms(options))
			{
				if (!File.Exists(exePath))
				{
					this._logger.LogWarning("Decompiler {Tool} was not found", exePath);
					output.Append("[Error] <none>:0: Required tool not found: ").Append(exePath).Append(LineBreak);
					continue;
				}

				ProcessRunResult result;
				try
				{
					result = await this._processRunner.RunAsync(
						new ProcessRunRequest
						{
							FileName = exePath,
							Arguments = commandLines.BuildDecompiler(request.ScriptPath, paths.DecompilationFile),
							OutputLineProgress = outputProgress,
						},
						cancellationToken).ConfigureAwait(false);
				}
				catch (Win32Exception exception)
				{
					this._logger.LogError(exception, "Failed to start decompiler {Tool}", exePath);
					output.Append("[Error] <none>:0: Failed to start ").Append(exePath).Append(": ").Append(exception.Message).Append(LineBreak);
					continue;
				}

				output.Append(result.Output);
				if (result.ExitCode == 0)
				{
					succeeded = true;
					break;
				}
			}

			return DecompilationOutputFiles.Complete(paths, request, output.ToString(), succeeded);
		}
	}
}
