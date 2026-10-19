namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.IO;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Resolves the intermediate and output file locations used by the compiler services.
	/// </summary>
	public sealed class CompilerPaths
	{
		private const String PreprocessSuffix = "_[pre]";
		private const String HashSeparator = "~";

		private static readonly Regex _intermediateName = new Regex(
			@"^(?<name>.*)_\[pre\](?:~[0-9A-F]{8})?$",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

		private readonly CompilerOptions _options;

		/// <summary>
		/// Initializes a new instance of the <see cref="CompilerPaths"/> class.
		/// </summary>
		/// <param name="options">The compiler options.</param>
		public CompilerPaths(CompilerOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);
			this._options = options;
		}

		/// <summary>Gets the common file the preprocessors write their result to.</summary>
		public String PreprocessFile => Path.Combine(this._options.ScratchDirectory, "preprocess.ssl");

		/// <summary>Gets the file the decompiler writes its result to.</summary>
		public String DecompilationFile => Path.Combine(this._options.ScratchDirectory, "decomp.ssl");

		/// <summary>
		/// Gets the path of the compiled <c>.int</c> file for a script.
		/// </summary>
		/// <param name="scriptFile">The script (or, for a preprocessed intermediate script, the <c>_[pre].ssl</c> copy).</param>
		/// <param name="sourceDirectory">The folder of the original script, or an empty string when <paramref name="scriptFile"/> is the original.</param>
		/// <param name="temporaryOutput">Whether the result goes to the scratch folder (round-trip check).</param>
		/// <returns>The full path of the <c>.int</c> file.</returns>
		/// <exception cref="InvalidOperationException">The output folder is required but not configured.</exception>
		public String GetOutputPath(String scriptFile, String sourceDirectory = "", Boolean temporaryOutput = false)
		{
			String outputFile = Path.GetFileNameWithoutExtension(scriptFile);
			if (sourceDirectory.Length != 0 && this._options.Preprocessor != PreprocessorKind.Builtin)
			{
				Match intermediate = _intermediateName.Match(outputFile);
				if (intermediate.Success)
				{
					outputFile = intermediate.Groups["name"].Value;
				}
			}

			outputFile += ".int";

			if (temporaryOutput)
			{
				return Path.Combine(this._options.ScratchDirectory, outputFile);
			}

			if (this._options.CompileToSourceDirectory)
			{
				if (sourceDirectory.Length == 0)
				{
					sourceDirectory = Path.GetDirectoryName(scriptFile) ?? String.Empty;
				}

				return Path.Combine(sourceDirectory, outputFile);
			}

			if (String.IsNullOrEmpty(this._options.OutputDirectory))
			{
				throw new InvalidOperationException("The output folder for compiled scripts is not set.");
			}

			return Path.Combine(this._options.OutputDirectory, outputFile);
		}

		/// <summary>
		/// Gets the path of the intermediate preprocessed script that is fed to sslc (<c>name_[pre]~XXXXXXXX.ssl</c>). The suffix is a hash
		/// of the full path of the script: a batch compilation walks the sub-folders and compiles in parallel, so two scripts of the same
		/// name in different folders must not share (and delete under each other) one intermediate file.
		/// </summary>
		/// <param name="scriptFile">The original script.</param>
		/// <returns>The full path in the scratch folder.</returns>
		public String GetIntermediatePreprocessedPath(String scriptFile)
		{
			String fullPath = Path.GetFullPath(scriptFile);
			Byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant()));
			String name = Path.GetFileNameWithoutExtension(fullPath) + PreprocessSuffix + HashSeparator + Convert.ToHexString(hash, 0, 4);
			return Path.Combine(this._options.ScratchDirectory, name + ".ssl");
		}

		/// <summary>Gets the path of the preprocessed script offered to the user (<c>name_[preproc].ssl</c>).</summary>
		/// <param name="scriptFile">The original script (only the name is used).</param>
		/// <returns>The full path in the scratch folder.</returns>
		public String GetPreprocessedCopyPath(String scriptFile)
		{
			return Path.Combine(this._options.ScratchDirectory, Path.GetFileNameWithoutExtension(scriptFile) + "_[preproc].ssl");
		}

		/// <summary>Gets the path of a decompiled script.</summary>
		/// <param name="intFile">The compiled script.</param>
		/// <param name="temporaryOutput">Whether the result belongs to a round-trip check.</param>
		/// <returns>The full path in the scratch folder.</returns>
		public String GetDecompiledPath(String intFile, Boolean temporaryOutput)
		{
			String name = Path.GetFileNameWithoutExtension(intFile);
			return Path.Combine(this._options.ScratchDirectory, name + (temporaryOutput ? "_[temp].ssl" : "_[decomp].ssl"));
		}
	}
}