namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Core.Configuration;
	using Core.Dialogue;
	using Core.Messages;
	using Core.Parsing;
	using Documents;
	using Localization;
	using Problems;

	/// <summary>
	/// Compares a script with its message file (<see cref="ScriptMessageConsistency"/>) and lists the differences as problems: the numbers the
	/// script uses and the file lacks (a click goes to the script line), the unused, duplicate and empty messages (a click goes to the line of the
	/// message file). It runs on demand (Dialogue &gt; Check Messages) and, with the setting <c>CheckMessagesOnSave</c>, a short moment after a script
	/// that has a message file, or that message file, was saved. The work is done off the UI thread and a newer save cancels the pending one.
	/// </summary>
	public sealed class MessageConsistencyService : IDisposable
	{
		/// <summary>The quiet period after a save before the check starts.</summary>
		public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(800);

		private const Int32 MaxOtherFiles = 8;
		private const Int32 MaxNotes = 500;

		private readonly ISettingsService _settings;
		private readonly IMessageFileService _files;
		private readonly IDialogueRulesSource _rules;
		private readonly IProblemService _problems;
		private readonly TimeSpan _debounce;
		private readonly Dictionary<DocumentViewModel, CancellationTokenSource> _pending = [];
		private IWorkspaceManager? _workspace;

		/// <summary>Initializes the service.</summary>
		/// <param name="settings">Supplies the message file options and the <c>CheckMessagesOnSave</c> setting.</param>
		/// <param name="files">Finds, reads and appends to the message files.</param>
		/// <param name="rules">Supplies the dialogue function rules in effect.</param>
		/// <param name="problems">Receives the problems.</param>
		/// <param name="debounce">The quiet period after a save; <see cref="DefaultDebounce"/> when <see langword="null"/>.</param>
		public MessageConsistencyService(ISettingsService settings, IMessageFileService files, IDialogueRulesSource rules, IProblemService problems, TimeSpan? debounce = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(rules);
			ArgumentNullException.ThrowIfNull(problems);
			this._settings = settings;
			this._files = files;
			this._rules = rules;
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
		}

		/// <summary>Checks a script against its message file and replaces the problems of the previous check of that script.</summary>
		/// <param name="script">The script (saved, or at least named).</param>
		/// <param name="cancellationToken">Cancels the check; the problems are then left as they were.</param>
		/// <returns>The number of problems reported.</returns>
		public Task<Int32> CheckAsync(DocumentViewModel script, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(script);
			this.CancelPending(script);
			return this.CheckCoreAsync(script, cancellationToken);
		}

		private async Task<Int32> CheckCoreAsync(DocumentViewModel script, CancellationToken cancellationToken)
		{
			IWorkspaceManager workspace = this.RequireWorkspace();
			if (script.Kind != DocumentKind.Script || script.FilePath is null)
			{
				return 0;
			}

			if (script.ProgramInfo is null || script.NeedsParse)
			{
				await workspace.RequestParseAsync(script, true);
			}

			ProgramInfo? info = script.ProgramInfo;
			if (info is null)
			{
				return 0;
			}

			String scriptPath = script.FilePath;
			String? messagePath = script.MessageFilePath;
			if (messagePath is null || !File.Exists(messagePath))
			{
				messagePath = await workspace.AssociateMessageFileAsync(script, false, false);
			}

			MessageFileOptions options = this._settings.Effective.ToMessageFileOptions();
			MessageDocument? own = null;
			if (messagePath is not null)
			{
				try
				{
					own = await this.ReadAsync(workspace, messagePath, options.Encoding, cancellationToken);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this.Replace(script, new[] { ProblemFactory.OpenFailed(messagePath, ex) with { Source = ProblemSource.Message, Origin = ProblemOrigins.MessageConsistency, Owner = script } });
					return 1;
				}
			}

			String scriptText = script.TextDocument.Text;
			DialogueFunctionRules rules = this._rules.Current;
			ScriptMessageOptions checkOptions = new();
			ScriptMessageReport report = await Task.Run(() => this.ScanAndCheckAsync(scriptText, info, rules, scriptPath, own, options, checkOptions, cancellationToken), cancellationToken);

			cancellationToken.ThrowIfCancellationRequested();
			if (!workspace.Documents.Contains(script))
			{
				return 0;
			}

			List<Problem> problems = CreateProblems(report, script, scriptPath, messagePath, checkOptions.FemaleOffset);
			this.Replace(script, problems);
			return problems.Count;
		}

		/// <summary>Appends the missing messages to the message file of a script, asking to create the file when there is none, and checks again.</summary>
		/// <param name="scriptPath">The script.</param>
		/// <param name="numbers">The message numbers to add.</param>
		/// <returns>A task that completes when the numbers are added and the problems refreshed.</returns>
		public async Task AddMissingMessagesAsync(String scriptPath, IReadOnlyList<Int32> numbers)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			ArgumentNullException.ThrowIfNull(numbers);
			IWorkspaceManager workspace = this.RequireWorkspace();

			DocumentViewModel? script = workspace.FindDocument(scriptPath) ?? await workspace.OpenAsync(scriptPath);
			if (script is null)
			{
				return;
			}

			String? messagePath = script.MessageFilePath is { } known && File.Exists(known) ? known : await workspace.AssociateMessageFileAsync(script, true, true);
			if (messagePath is null)
			{
				return;
			}

			Encoding encoding = this._settings.Effective.ToMessageFileOptions().Encoding;
			DocumentViewModel? tab = workspace.FindDocument(messagePath);
			Int32 added;
			try
			{
				added = tab is { IsModified: true }
					? AppendToOpenText(tab, numbers)
					: await this.AppendToFileAsync(workspace, messagePath, numbers, encoding, tab);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._problems.Report(ProblemFactory.SaveFailed(messagePath, ex, script) with { Source = ProblemSource.Message });
				workspace.ReportFailure(Loc.Format("Str.Shell.Problem.SaveFailed", Path.GetFileName(messagePath), ex.Message));
				return;
			}

			workspace.ReportStatus(Loc.Format(tab is { IsModified: true } ? "Str.Shell.Status.MessagesAddedToTab" : "Str.Shell.Status.MessagesAdded", added, Path.GetFileName(messagePath)));
			await workspace.RefreshMessageTextsAsync(script);
			await this.CheckAsync(script);
		}

		/// <summary>Finds the message file of a script, offering to create it, associates it with the script and checks the script again.</summary>
		/// <param name="scriptPath">The script.</param>
		/// <returns>A task that completes when the file is associated and the problems refreshed.</returns>
		public async Task CreateMessageFileAsync(String scriptPath)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			IWorkspaceManager workspace = this.RequireWorkspace();

			DocumentViewModel? script = workspace.FindDocument(scriptPath) ?? await workspace.OpenAsync(scriptPath);
			if (script is not null && await workspace.AssociateMessageFileAsync(script, true, true) is not null)
			{
				await this.CheckAsync(script);
			}
		}

		/// <summary>Cancels the checks that wait for the end of their quiet period.</summary>
		public void Dispose()
		{
			if (this._workspace is not null)
			{
				this._workspace.DocumentSaved -= this.OnDocumentSaved;
				this._workspace.DocumentClosed -= this.OnDocumentClosed;
			}

			foreach (CancellationTokenSource source in this._pending.Values)
			{
				source.Cancel();
				source.Dispose();
			}

			this._pending.Clear();
		}

		private static Int32 AppendToOpenText(DocumentViewModel tab, IReadOnlyList<Int32> numbers)
		{
			MessageDocument existing = MessageDocument.Parse(tab.TextDocument.Text.Split('\n').Select(line => line.TrimEnd('\r')).ToList());
			List<Int32> added = numbers.Where(number => number > 0).Distinct().Order().Where(number => existing.FindMessage(number) is null).ToList();
			if (added.Count > 0)
			{
				String text = tab.TextDocument.Text;
				Int32 end = text.TrimEnd().Length;
				String lines = String.Join("\r\n", added.Select(number => MessageEntry.CreateMessage(number).Format()));
				tab.TextDocument.Insert(end, (end > 0 ? "\r\n" : String.Empty) + lines);
			}

			return added.Count;
		}

		private async Task<Int32> AppendToFileAsync(IWorkspaceManager workspace, String messagePath, IReadOnlyList<Int32> numbers, Encoding encoding, DocumentViewModel? tab)
		{
			IReadOnlyList<Int32> added = await this._files.AppendMessagesAsync(messagePath, numbers, encoding);
			if (tab is not null && added.Count > 0)
			{
				// The text tab of the message file shows what was written; a Message Editor window notices the new time stamp by itself.
				await workspace.ReloadAsync(tab);
			}

			return added.Count;
		}

		private void OnDocumentSaved(Object? sender, DocumentViewModel document)
		{
			IWorkspaceManager? workspace = this._workspace;
			if (workspace is null || !this._settings.Settings.Messages.CheckMessagesOnSave)
			{
				return;
			}

			if (document.Kind == DocumentKind.Script)
			{
				if (document.MessageFilePath is not null)
				{
					this.Schedule(workspace, document);
				}

				return;
			}

			if (document.Kind == DocumentKind.Message && document.FilePath is not null)
			{
				String full = DocumentPaths.GetFullPath(document.FilePath);
				foreach (DocumentViewModel script in workspace.Documents.Where(d => d.Kind == DocumentKind.Script && d.MessageFilePath is not null).ToList())
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

		private async Task<MessageDocument> ReadAsync(IWorkspaceManager workspace, String path, Encoding encoding, CancellationToken cancellationToken)
		{
			if (workspace.GetOpenDocumentTexts().TryGetValue(DocumentPaths.GetFullPath(path), out String? text))
			{
				return MessageDocument.Parse(text.Split('\n').Select(line => line.TrimEnd('\r')).ToList());
			}

			return await this._files.ReadAsync(path, encoding, cancellationToken);
		}

		private async Task<ScriptMessageReport> ScanAndCheckAsync(String scriptText, ProgramInfo info, DialogueFunctionRules rules, String scriptPath, MessageDocument? own, MessageFileOptions options, ScriptMessageOptions checkOptions, CancellationToken cancellationToken)
		{
			ScriptMessageScan scan = ScriptMessageScanner.Scan(scriptText, info, rules);
			Dictionary<Int32, MessageDocument> others = [];
			foreach (Int32 number in scan.OtherFileNumbers.Where(n => n > 0).Take(MaxOtherFiles))
			{
				cancellationToken.ThrowIfCancellationRequested();
				MessageFileLocation location = this._files.LocateById(scriptPath, number, options);
				if (location.Found && location.Path is not null)
				{
					try
					{
						others[number] = await this._files.ReadAsync(location.Path, options.Encoding, cancellationToken);
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						// A file that cannot be read is simply not checked.
					}
				}
			}

			return ScriptMessageConsistency.Check(scan, own, others, checkOptions);
		}

		private static List<Problem> CreateProblems(ScriptMessageReport report, DocumentViewModel script, String scriptPath, String? messagePath, Int32 femaleOffset)
		{
			List<Problem> problems = [];
			Int32 unused = 0;
			Int32 female = 0;
			foreach (ScriptMessageFinding finding in report.Findings)
			{
				// A message file with thousands of messages that nothing uses must not bury the real problems.
				if ((finding.Kind == ScriptMessageFindingKind.UnusedMessage && ++unused > MaxNotes) || (finding.Kind == ScriptMessageFindingKind.MissingFemaleVariant && ++female > MaxNotes))
				{
					continue;
				}

				problems.Add(ProblemFactory.ScriptMessage(finding, scriptPath, messagePath, report.MissingNumbers, femaleOffset, script));
			}

			return problems;
		}

		private void Replace(DocumentViewModel script, IEnumerable<Problem> problems)
		{
			this._problems.Remove(problem => ReferenceEquals(problem.Owner, script) && problem.Origin == ProblemOrigins.MessageConsistency);
			this._problems.ReportRange(problems);
		}

		private IWorkspaceManager RequireWorkspace()
		{
			return this._workspace ?? throw new InvalidOperationException("The message consistency service is not attached to a workspace.");
		}
	}
}