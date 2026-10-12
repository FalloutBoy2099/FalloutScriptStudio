namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// Default <see cref="IScriptParser"/>: reproduces the logic of the legacy <c>ParserExternal.Parse</c> and of the
	/// parse management of the editor window.
	/// </summary>
	/// <remarks>
	/// <list type="number">
	/// <item>When the native parser is enabled and the script has a file path, it is asked first.</item>
	/// <item>If it succeeds, its procedures and variables are the new data.</item>
	/// <item>Otherwise (disabled, unavailable, failed, or the script has errors) the internal parser is used: it parses from
	/// scratch when the native parser is disabled and no previous data exists, and it updates the previous data
	/// (keeping references and local variables) otherwise.</item>
	/// <item>Macros of the script and its includes are always collected; global macros are reloaded on the first pass.</item>
	/// </list>
	/// The previous <see cref="ProgramInfo"/> is never modified, so results can be produced on a background thread.
	/// </remarks>
	public sealed class ScriptParser : IScriptParser
	{
		private const String NativeSource = "Native Parser";

		private readonly INativeParser _nativeParser;
		private readonly Func<ParserOptions> _optionsProvider;
		private readonly InternalParser _internalParser;
		private readonly ILogger _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptParser"/> class.
		/// </summary>
		/// <param name="nativeParser">The native parser.</param>
		/// <param name="optionsProvider">Returns the current options; called once per parse so options can change at runtime.</param>
		/// <param name="globalMacros">The registry of global macros; a new one is created when <c>null</c>.</param>
		/// <param name="logger">Optional logger.</param>
		public ScriptParser(INativeParser nativeParser, Func<ParserOptions> optionsProvider, MacroRegistry? globalMacros = null, ILogger<ScriptParser>? logger = null)
		{
			this._nativeParser = nativeParser ?? throw new ArgumentNullException(nameof(nativeParser));
			this._optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));
			this.GlobalMacros = globalMacros ?? new MacroRegistry();
			this._internalParser = new InternalParser();
			this._logger = logger ?? NullLogger<ScriptParser>.Instance;
		}

		/// <inheritdoc />
		public MacroRegistry GlobalMacros { get; }

		/// <inheritdoc />
		public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			cancellationToken.ThrowIfCancellationRequested();
			ParserOptions options = this._optionsProvider();
			var diagnostics = new ParseDiagnostics();

			String? globalDirectory = options.EffectiveGlobalMacroDirectory;
			if (globalDirectory is not null
				&& (request.IsFirstPass || !String.Equals(this.GlobalMacros.SourceDirectory, globalDirectory, StringComparison.OrdinalIgnoreCase)))
			{
				await Task.Run(() => this.GlobalMacros.LoadFromDirectory(globalDirectory, diagnostics, true), cancellationToken).ConfigureAwait(false);
			}

			NativeParseResult? native = null;
			if (options.EnableNativeParser && !String.IsNullOrEmpty(request.FilePath))
			{
				native = await this._nativeParser.ParseAsync(request.ScriptText, request.FilePath, options, cancellationToken).ConfigureAwait(false);
			}

			ParseOutcome outcome = GetOutcome(native);
			ProgramInfo info = await Task.Run(() => this.BuildProgramInfo(request, options, native, outcome, diagnostics), cancellationToken).ConfigureAwait(false);

			this._logger.Parsed(request.FilePath ?? "<unsaved>", outcome, native?.ExitCode ?? -1);

			return new ParseResult(info, outcome)
			{
				NativeStatusCode = native?.ExitCode ?? -1,
				NativeLog = native?.Log ?? String.Empty,
				Message = native?.Message,
				Diagnostics = diagnostics.ToArray(),
				LogText = BuildLogText(options, native, diagnostics),
			};
		}

		/// <inheritdoc />
		public Task ReloadGlobalMacrosAsync(CancellationToken cancellationToken)
		{
			String? directory = this._optionsProvider().EffectiveGlobalMacroDirectory;
			if (directory is null)
			{
				return Task.CompletedTask;
			}

			return Task.Run(() => this.GlobalMacros.LoadFromDirectory(directory), cancellationToken);
		}

		private static ParseOutcome GetOutcome(NativeParseResult? native)
		{
			return native switch
			{
				null => ParseOutcome.NativeDisabled,
				{ Succeeded: true } => ParseOutcome.NativeSucceeded,
				{ Status: NativeParseStatus.Unavailable } => ParseOutcome.NativeUnavailable,
				{ Status: NativeParseStatus.Failed } => ParseOutcome.NativeFailed,
				_ => ParseOutcome.NativeReportedErrors,
			};
		}

		private static String BuildLogText(ParserOptions options, NativeParseResult? native, ParseDiagnostics diagnostics)
		{
			if (!options.EnableNativeParser)
			{
				return diagnostics.ToLogText(ParseDiagnosticSeverity.Warning);
			}

			String log = native?.Log ?? String.Empty;
			if (native?.Message is not null)
			{
				if (log.Length > 0 && !log.EndsWith('\n'))
				{
					log += "\r\n";
				}

				log += new ParseDiagnostic(ParseDiagnosticSeverity.Warning, NativeSource, -1, native.Message) + "\r\n";
			}

			return log;
		}

		private static String? GetFullPath(String? path)
		{
			if (String.IsNullOrEmpty(path))
			{
				return null;
			}

			try
			{
				return Path.GetFullPath(path);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return path;
			}
		}

		private ProgramInfo BuildProgramInfo(ParseRequest request, ParserOptions options, NativeParseResult? native, ParseOutcome outcome, ParseDiagnostics diagnostics)
		{
			String? filePath = request.FilePath;
			String? fullPath = GetFullPath(filePath);
			ProgramInfo info;
			Boolean keepMacros = false;

			if (native is { HasData: true })
			{
				// Status -1 (semantic errors) still delivers the procedures and variables; the script is flagged as having errors.
				info = new ProgramInfo(native.Procedures, native.Variables, null, this.GlobalMacros, fullPath)
					.WithFlags(isParsed: true, hasNativeData: true, hasParseError: !native.Succeeded, needsDataRefresh: false);
			}
			else
			{
				ProgramInfo? previous = request.PreviousInfo;
				if (!options.EnableNativeParser || previous is null || !previous.IsParsed)
				{
					info = this._internalParser.Parse(request.ScriptText, filePath, this.GlobalMacros, diagnostics);
				}
				else if (previous.NeedsDataRefresh || PathChanged(previous, fullPath))
				{
					info = fullPath is null ? previous : this._internalParser.RebindToFile(previous, fullPath);
					keepMacros = true;
				}
				else
				{
					info = this._internalParser.MergeProcedures(previous, request.ScriptText, filePath, diagnostics);
				}

				Boolean parseError = outcome is ParseOutcome.NativeReportedErrors or ParseOutcome.NativeFailed;
				info = info.WithFlags(isParsed: true, hasParseError: parseError, needsDataRefresh: false);
			}

			if (!keepMacros)
			{
				var collector = new MacroCollector(options.CreateIncludeResolver());
				info = info.WithScriptMacros(collector.CollectFromText(request.ScriptText, filePath, true, diagnostics));
			}

			return info.WithGlobalMacros(this.GlobalMacros).WithScriptFilePath(fullPath);
		}

		private static Boolean PathChanged(ProgramInfo previous, String? fullPath)
		{
			return previous.ScriptFilePath is not null
				&& fullPath is not null
				&& !String.Equals(previous.ScriptFilePath, fullPath, StringComparison.OrdinalIgnoreCase);
		}
	}
}
