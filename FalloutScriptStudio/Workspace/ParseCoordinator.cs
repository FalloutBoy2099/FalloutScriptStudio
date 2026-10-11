namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>
	/// Keeps the parse data of open scripts up to date: re-parses a document a short time after its last edit, cancels
	/// superseded runs, and copies the result (program info, log, errors, status) into the <see cref="DocumentViewModel"/>.
	/// </summary>
	public sealed class ParseCoordinator
	{
		/// <summary>The default pause after the last edit before a document is parsed.</summary>
		public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(500);

		private readonly IScriptParser _parser;
		private readonly ISettingsService _settings;
		private readonly ILogger _logger;
		private readonly ParserLogParser _logParser = new ParserLogParser();
		private readonly Dictionary<DocumentViewModel, CancellationTokenSource> _pending = new Dictionary<DocumentViewModel, CancellationTokenSource>();
		private readonly Dictionary<DocumentViewModel, EventHandler> _textHandlers = new Dictionary<DocumentViewModel, EventHandler>();

		/// <summary>Initializes the coordinator.</summary>
		public ParseCoordinator(IScriptParser parser, ISettingsService settings, ILogger<ParseCoordinator>? logger = null, TimeSpan? debounce = null)
		{
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(settings);
			this._parser = parser;
			this._settings = settings;
			this._logger = logger ?? NullLogger<ParseCoordinator>.Instance;
			this.Debounce = debounce ?? DefaultDebounce;
		}

		/// <summary>Raised on the UI thread after a document's parse data was refreshed.</summary>
		public event EventHandler<DocumentViewModel>? Parsed;

		/// <summary>Gets or sets the pause after the last edit before a document is parsed.</summary>
		public TimeSpan Debounce { get; set; }

		/// <summary>Starts watching the text of a document: every edit schedules a debounced parse.</summary>
		public void Attach(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (this._textHandlers.ContainsKey(document))
			{
				return;
			}

			EventHandler handler = (_, _) =>
			{
				if (document.IsParsable)
				{
					_ = this.ParseAsync(document, false);
				}
			};
			this._textHandlers[document] = handler;
			document.TextDocument.TextChanged += handler;
		}

		/// <summary>Stops watching a document and cancels its pending parse.</summary>
		public void Detach(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (this._textHandlers.Remove(document, out EventHandler? handler))
			{
				document.TextDocument.TextChanged -= handler;
			}

			this.Cancel(document);
		}

		/// <summary>Reloads the macros of the headers folder.</summary>
		public Task ReloadGlobalMacrosAsync()
		{
			return this._parser.ReloadGlobalMacrosAsync(CancellationToken.None);
		}

		/// <summary>Cancels the pending or running parse of a document.</summary>
		public void Cancel(DocumentViewModel document)
		{
			if (this._pending.Remove(document, out CancellationTokenSource? source))
			{
				source.Cancel();
			}
		}

		/// <summary>Gets a value indicating whether a parse of the document is waiting or running.</summary>
		public Boolean IsPending(DocumentViewModel document)
		{
			return this._pending.ContainsKey(document);
		}

		/// <summary>Parses a document (after the debounce delay unless <paramref name="immediate"/>); the task ends when the run finished or was superseded.</summary>
		/// <param name="document">The document.</param>
		/// <param name="immediate">True to skip the debounce delay.</param>
		/// <param name="firstPass">True for the first parse of a freshly opened document (reloads the global macros).</param>
		public async Task ParseAsync(DocumentViewModel document, Boolean immediate, Boolean firstPass = false)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (!document.IsParsable)
			{
				return;
			}

			this.Cancel(document);
			CancellationTokenSource source = new CancellationTokenSource();
			this._pending[document] = source;
			Boolean parserEnabled = this._settings.Settings.Editor.EnableParser;
			document.NeedsParse = true;
			document.ParserStatus = new ParserStatus(parserEnabled ? ParserStatusKind.Waiting : ParserStatusKind.OffMacros);

			try
			{
				if (!immediate && this.Debounce > TimeSpan.Zero)
				{
					await Task.Delay(this.Debounce, source.Token);
				}

				if (parserEnabled)
				{
					document.ParserStatus = new ParserStatus(ParserStatusKind.Running);
				}

				ParseRequest request = new ParseRequest(document.TextDocument.Text)
				{
					FilePath = document.FilePath,
					PreviousInfo = document.ProgramInfo,
					IsFirstPass = firstPass,
				};
				ParseResult result = await this._parser.ParseAsync(request, source.Token);
				source.Token.ThrowIfCancellationRequested();

				this.Apply(document, result, parserEnabled);
				this.Parsed?.Invoke(this, document);
			}
			catch (OperationCanceledException)
			{
				// Superseded by a newer request or the document was closed.
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Parsing '{File}' failed", document.FilePath ?? document.FileName);
				if (this._pending.TryGetValue(document, out CancellationTokenSource? current) && current == source)
				{
					document.ParserStatus = new ParserStatus(ParserStatusKind.FailedLog);
					document.NeedsParse = false;
				}
			}
			finally
			{
				if (this._pending.TryGetValue(document, out CancellationTokenSource? current) && current == source)
				{
					this._pending.Remove(document);
				}

				source.Dispose();
			}
		}

		private static BuildErrorType ToErrorType(ParseDiagnosticSeverity severity)
		{
			return severity switch
			{
				ParseDiagnosticSeverity.Error => BuildErrorType.Error,
				ParseDiagnosticSeverity.Warning => BuildErrorType.Warning,
				_ => BuildErrorType.Message,
			};
		}

		private static ParserStatus DescribeStatus(ParseResult result, Boolean parserEnabled)
		{
			if (!parserEnabled)
			{
				return new ParserStatus(ParserStatusKind.OffMacros);
			}

			if (result.HasParseError)
			{
				return new ParserStatus(ParserStatusKind.SyntaxError);
			}

			return new ParserStatus(result.Outcome switch
			{
				ParseOutcome.NativeUnavailable => ParserStatusKind.Builtin,
				ParseOutcome.NativeFailed => ParserStatusKind.FailedParserLog,
				ParseOutcome.NativeReportedErrors => ParserStatusKind.SyntaxError,
				ParseOutcome.NativeDisabled => ParserStatusKind.OffMacros,
				_ => ParserStatusKind.Done,
			});
		}

		private void Apply(DocumentViewModel document, ParseResult result, Boolean parserEnabled)
		{
			Boolean includeWarnings = this._settings.Settings.Editor.ParserWarnings;
			List<BuildError> errors = new List<BuildError>();
			String logText = result.LogText;

			if (result.Outcome is ParseOutcome.NativeSucceeded or ParseOutcome.NativeReportedErrors)
			{
				ParserLogResult parsed = this._logParser.Parse(result.LogText, document.FileName, includeWarnings, DateTime.Now);
				logText = parsed.Text;
				errors.AddRange(parsed.Errors);
				errors.AddRange(parsed.IncludeNotes);
			}
			else
			{
				String file = document.FilePath ?? BuildError.NoFile;
				foreach (ParseDiagnostic diagnostic in result.Diagnostics.Where(d => d.Severity == ParseDiagnosticSeverity.Error
					|| (includeWarnings && d.Severity == ParseDiagnosticSeverity.Warning)))
				{
					errors.Add(new BuildError(ToErrorType(diagnostic.Severity), diagnostic.Message, file, diagnostic.Line));
				}

				if (!String.IsNullOrEmpty(logText))
				{
					logText = "------ Script: " + document.FileName + " < Parse Time: " + DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " > ------\r\n" + logText;
				}
			}

			document.ProgramInfo = result.ProgramInfo;
			document.ParserLog = logText;
			document.ParserErrors = errors;
			document.NeedsParse = false;
			document.ParserStatus = DescribeStatus(result, parserEnabled);
		}
	}
}
