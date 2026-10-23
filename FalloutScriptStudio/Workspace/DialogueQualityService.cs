namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using ICSharpCode.AvalonEdit.Document;
	using Core.Configuration;
	using Core.Dialogue;
	using Core.Messages;
	using Core.Parsing;
	using Documents;
	using Problems;

	/// <summary>
	/// Checks the structure of the dialogue of a script (<see cref="DialogueQualityChecker"/>) and lists the findings as problems (source Parse, a click
	/// goes to the line of the script). It runs on demand (Dialogue &gt; Check Dialogue) and, with the setting <c>CheckDialogueOnSave</c>, a short moment
	/// after a script that has dialogue nodes, or the message file of such a script, was saved. The work is done off the UI thread and a newer save
	/// cancels the pending one. The findings of a script are dropped when it is parsed again, so they are published again after the parse as long as the
	/// text did not change; they are removed when the script is closed.
	/// </summary>
	public sealed class DialogueQualityService : IDialogueQualityAnalyzer, IDisposable
	{
		/// <summary>The quiet period after a save before the check starts.</summary>
		public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(800);

		private readonly ISettingsService _settings;
		private readonly IDialogueNodeParser _parser;
		private readonly IDialogueRulesSource _rules;
		private readonly IMessageFileService _files;
		private readonly IProblemService _problems;
		private readonly TimeSpan _debounce;
		private readonly Dictionary<DocumentViewModel, CancellationTokenSource> _pending = [];
		private readonly Dictionary<DocumentViewModel, Published> _published = [];
		private IWorkspaceManager? _workspace;

		/// <summary>Initializes the service.</summary>
		/// <param name="settings">Supplies the <c>CheckDialogueOnSave</c> setting and the message file options.</param>
		/// <param name="parser">The dialogue parser (the one of the flowchart and the preview).</param>
		/// <param name="rules">Supplies the dialogue function rules in effect (for the message numbers).</param>
		/// <param name="files">Reads the message file of the script.</param>
		/// <param name="problems">Receives the problems.</param>
		/// <param name="debounce">The quiet period after a save; <see cref="DefaultDebounce"/> when <see langword="null"/>.</param>
		public DialogueQualityService(ISettingsService settings, IDialogueNodeParser parser, IDialogueRulesSource rules, IMessageFileService files, IProblemService problems, TimeSpan? debounce = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(rules);
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(problems);
			this._settings = settings;
			this._parser = parser;
			this._rules = rules;
			this._files = files;
			this._problems = problems;
			this._debounce = debounce ?? DefaultDebounce;
		}

		/// <summary>Connects the service to the workspace whose saves it follows. Called once by the workspace.</summary>
		/// <param name="workspace">The workspace.</param>
		public void Attach(IWorkspaceManager workspace)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			this._workspace = workspace;
			workspace.DocumentSaved += this.OnDocumentSaved;
			workspace.DocumentClosed += this.OnDocumentClosed;
			workspace.DocumentParsed += this.OnDocumentParsed;
		}

		/// <summary>Checks the dialogue of a script and replaces the problems of the previous check of that script.</summary>
		/// <param name="script">The script (it must have a file).</param>
		/// <param name="cancellationToken">Cancels the check; the problems are then left as they were.</param>
		/// <returns>Whether the script has a dialogue, and the number of problems reported.</returns>
		public Task<DialogueCheckResult> CheckAsync(DocumentViewModel script, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(script);
			this.CancelPending(script);
			return this.CheckCoreAsync(script, cancellationToken);
		}

		/// <inheritdoc />
		public async Task<DialogueQualityReport> AnalyzeAsync(DocumentViewModel script, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(script);
			return (await this.AnalyzeCoreAsync(script, cancellationToken))?.Report ?? DialogueQualityReport.Empty;
		}

		/// <summary>Cancels the checks that wait for the end of their quiet period.</summary>
		public void Dispose()
		{
			if (this._workspace is not null)
			{
				this._workspace.DocumentSaved -= this.OnDocumentSaved;
				this._workspace.DocumentClosed -= this.OnDocumentClosed;
				this._workspace.DocumentParsed -= this.OnDocumentParsed;
			}

			foreach (CancellationTokenSource source in this._pending.Values)
			{
				source.Cancel();
				source.Dispose();
			}

			this._pending.Clear();
			this._published.Clear();
		}

		/// <summary>Determines whether a text may hold a dialogue (it mentions a node or the talk handler): the cheap test that decides whether a save is worth a check.</summary>
		/// <param name="text">The text of the script.</param>
		/// <returns><see langword="true"/> when a check is worth running.</returns>
		public static Boolean MayHaveDialogue(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			return text.Contains("node", StringComparison.OrdinalIgnoreCase) || text.Contains(DialogueNodeRule.TalkProcedure, StringComparison.OrdinalIgnoreCase);
		}

		private async Task<DialogueCheckResult> CheckCoreAsync(DocumentViewModel script, CancellationToken cancellationToken)
		{
			IWorkspaceManager workspace = this.RequireWorkspace();
			if (script.Kind != DocumentKind.Script || script.FilePath is null)
			{
				return default;
			}

			(DialogueQualityReport Report, ITextSourceVersion? Version)? analysis = await this.AnalyzeCoreAsync(script, cancellationToken);
			if (analysis is not { } done)
			{
				return default;
			}

			DialogueQualityReport report = done.Report;

			cancellationToken.ThrowIfCancellationRequested();
			if (!workspace.Documents.Contains(script))
			{
				return default;
			}

			List<Problem> problems = report.Findings.Select(finding => ProblemFactory.DialogueQuality(finding, script.FilePath, script)).ToList();
			this.Replace(script, problems, done.Version);
			return new DialogueCheckResult(report.HasDialogue, problems.Count);
		}

		private async Task<(DialogueQualityReport Report, ITextSourceVersion? Version)?> AnalyzeCoreAsync(DocumentViewModel script, CancellationToken cancellationToken)
		{
			IWorkspaceManager workspace = this.RequireWorkspace();
			if (script.Kind != DocumentKind.Script)
			{
				return null;
			}

			if (script.ProgramInfo is null || script.NeedsParse)
			{
				await workspace.RequestParseAsync(script, true);
			}

			ProgramInfo? info = script.ProgramInfo;
			if (info is null)
			{
				return null;
			}

			String text = script.TextDocument.Text;
			ITextSourceVersion? version = script.TextDocument.Version;
			DialogueFunctionRules rules = this._rules.Current;
			MessageDocument? messages = await this.ReadMessagesAsync(workspace, script, cancellationToken);
			DialogueQualityReport report = await Task.Run(
				() =>
				{
					ScriptMessageReport? messageReport = messages is null ? null : ScriptMessageConsistency.Check(text, info, rules, messages);
					return DialogueQualityChecker.Check(text, info, this._parser, null, messageReport);
				},
				cancellationToken);
			return (report, version);
		}

		private async Task<MessageDocument?> ReadMessagesAsync(IWorkspaceManager workspace, DocumentViewModel script, CancellationToken cancellationToken)
		{
			String? path = script.MessageFilePath;
			if (path is null)
			{
				return null;
			}

			try
			{
				if (workspace.GetOpenDocumentTexts().TryGetValue(DocumentPaths.GetFullPath(path), out String? open))
				{
					return MessageDocument.Parse(open.Split('\n').Select(line => line.TrimEnd('\r')).ToList());
				}

				if (!File.Exists(path))
				{
					return null;
				}

				Encoding encoding = this._settings.Effective.ToMessageFileOptions().Encoding;
				return await this._files.ReadAsync(path, encoding, cancellationToken);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// The message numbers are only a link to the other check: without the file the dialogue is checked without them.
				return null;
			}
		}

		private void OnDocumentSaved(Object? sender, DocumentViewModel document)
		{
			IWorkspaceManager? workspace = this._workspace;
			if (workspace is null || !this._settings.Settings.Messages.CheckDialogueOnSave)
			{
				return;
			}

			if (document.Kind == DocumentKind.Script)
			{
				if (document.FilePath is not null && (MayHaveDialogue(document.TextDocument.Text) || this._published.ContainsKey(document)))
				{
					this.Schedule(workspace, document);
				}

				return;
			}

			if (document.Kind == DocumentKind.Message && document.FilePath is not null)
			{
				String full = DocumentPaths.GetFullPath(document.FilePath);
				foreach (DocumentViewModel script in workspace.Documents.Where(d => d.Kind == DocumentKind.Script && d.FilePath is not null && d.MessageFilePath is not null && (this._published.ContainsKey(d) || MayHaveDialogue(d.TextDocument.Text))).ToList())
				{
					if (String.Equals(DocumentPaths.GetFullPath(script.MessageFilePath!), full, StringComparison.OrdinalIgnoreCase))
					{
						this.Schedule(workspace, script);
					}
				}
			}
		}

		private void OnDocumentClosed(Object? sender, DocumentViewModel document)
		{
			this.CancelPending(document);
			this._published.Remove(document);
		}

		private void OnDocumentParsed(Object? sender, DocumentViewModel document)
		{
			// A parse drops the problems of source Parse; the findings stay true as long as the text is the one that was checked.
			if (this._published.TryGetValue(document, out Published? last) && last.Version is { } version && document.TextDocument.Version is { } current
				&& version.BelongsToSameDocumentAs(current) && version.CompareAge(current) == 0)
			{
				this._problems.ReportRange(last.Problems);
			}
		}

		private void Schedule(IWorkspaceManager workspace, DocumentViewModel script)
		{
			this.CancelPending(script);
			CancellationTokenSource source = new();
			this._pending[script] = source;
			_ = workspace.GuardAsync(() => this.RunDelayedAsync(script, source));
		}

		private async Task RunDelayedAsync(DocumentViewModel script, CancellationTokenSource source)
		{
			CancellationToken token = source.Token;
			try
			{
				await Task.Delay(this._debounce, token);
				await this.CheckCoreAsync(script, token);
			}
			catch (OperationCanceledException)
			{
				// A newer save, a manual check or the closing of the script took over.
			}
			finally
			{
				if (this._pending.TryGetValue(script, out CancellationTokenSource? current) && ReferenceEquals(current, source))
				{
					this._pending.Remove(script);
				}

				source.Dispose();
			}
		}

		private void CancelPending(DocumentViewModel script)
		{
			if (this._pending.Remove(script, out CancellationTokenSource? source))
			{
				source.Cancel();
			}
		}

		private void Replace(DocumentViewModel script, List<Problem> problems, ITextSourceVersion? version)
		{
			this._problems.Remove(problem => ReferenceEquals(problem.Owner, script) && problem.Origin == ProblemOrigins.DialogueQuality);
			this._problems.ReportRange(problems);
			if (problems.Count > 0)
			{
				this._published[script] = new Published(version, problems);
			}
			else
			{
				this._published.Remove(script);
			}
		}

		private IWorkspaceManager RequireWorkspace()
		{
			return this._workspace ?? throw new InvalidOperationException("The dialogue quality service is not attached to a workspace.");
		}

		private sealed record Published(ITextSourceVersion? Version, IReadOnlyList<Problem> Problems);
	}
}