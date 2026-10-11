namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>Decompiles compiled scripts for the workspace: runs the decompiler, fixes the result and asks where to save it.</summary>
	public sealed class ScriptDecompilationService
	{
		private readonly ISettingsService _settings;
		private readonly IScriptCompiler _compiler;
		private readonly IDialogService _dialogs;
		private readonly IOutputSink _output;
		private readonly DocumentFileService _files;

		/// <summary>Initializes the service.</summary>
		public ScriptDecompilationService(ISettingsService settings, IScriptCompiler compiler, IDialogService dialogs, IOutputSink output, DocumentFileService files)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(compiler);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(output);
			ArgumentNullException.ThrowIfNull(files);
			this._settings = settings;
			this._compiler = compiler;
			this._dialogs = dialogs;
			this._output = output;
			this._files = files;
		}

		/// <summary>
		/// Decompiles a <c>.int</c> file, logging to the Build tab. Unless the decompilation is temporary the user is asked where to
		/// save the source, and the source is written there.
		/// </summary>
		/// <returns>The decompiled script, or <see langword="null"/> (an error was already shown) when the decompilation failed.</returns>
		public async Task<DecompiledScript?> DecompileAsync(String path, OpenDocumentOptions options)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(options);

			if (options.ClearBuildLog)
			{
				this._output.SetBuildText(String.Empty);
			}

			this._output.ShowTab(OutputTab.Build);
			Progress<String> progress = new Progress<String>(line => this._output.AppendBuildLine(line));
			DecompilationResult result = await this._compiler.DecompileAsync(
				new DecompilationRequest
				{
					ScriptPath = path,
					Options = this._settings.Settings.ToCompilerOptions(this._settings.Paths),
					TemporaryOutput = options.TemporaryDecompile,
				},
				progress);

			if (!result.Success || result.OutputFilePath is null || !File.Exists(result.OutputFilePath))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.DecompileErrorTitle"), Loc.Format("Str.Shell.Dlg.DecompileFailed", path));
				return null;
			}

			DecodedText decoded = await this._files.ReadAsync(result.OutputFilePath);
			String text = new ProcedureBeginFixer().FixText(decoded.Text);

			String? target = null;
			if (!options.TemporaryDecompile)
			{
				target = this._dialogs.PickSaveFile(
					Loc.Get("Str.Shell.Dlg.SaveDecompiledTitle"),
					Loc.Get("Str.Shell.Filter.Script"),
					Path.GetDirectoryName(path),
					Path.GetFileNameWithoutExtension(path) + ".ssl");
				if (target is not null)
				{
					await File.WriteAllTextAsync(target, text, decoded.Encoding);
				}
			}

			return new DecompiledScript(text, decoded.Encoding, result.OutputFilePath, target);
		}
	}
}
