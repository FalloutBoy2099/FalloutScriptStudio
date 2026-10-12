namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>Associates scripts with their message files and keeps the message texts of every script for the hover and the diagnostics.</summary>
	public sealed class MessageAssociationService
	{
		private readonly ISettingsService _settings;
		private readonly IMessageFileService _messageFiles;
		private readonly IDialogService _dialogs;
		private readonly IMessageEditorService _messageEditor;
		private readonly ILogger _logger;
		private readonly IProblemService _problems;
		private readonly Dictionary<DocumentViewModel, Dictionary<Int32, String>> _messageTexts = new Dictionary<DocumentViewModel, Dictionary<Int32, String>>();
		private readonly ConditionalWeakTable<DocumentViewModel, Object> _forgotten = new ConditionalWeakTable<DocumentViewModel, Object>();

		/// <summary>Initializes the service.</summary>
		public MessageAssociationService(
			ISettingsService settings,
			IMessageFileService messageFiles,
			IDialogService dialogs,
			IMessageEditorService messageEditor,
			ILogger<MessageAssociationService>? logger = null,
			IProblemService? problems = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(messageFiles);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(messageEditor);
			this._settings = settings;
			this._messageFiles = messageFiles;
			this._dialogs = dialogs;
			this._messageEditor = messageEditor;
			this._logger = logger ?? NullLogger<MessageAssociationService>.Instance;
			this._problems = problems ?? new ProblemService();
		}

		/// <summary>Returns the text of a message of the message file associated with a script, or <see langword="null"/>.</summary>
		public String? GetMessageText(DocumentViewModel document, Int32 messageId)
		{
			ArgumentNullException.ThrowIfNull(document);
			return this._messageTexts.TryGetValue(document, out Dictionary<Int32, String>? map) && map.TryGetValue(messageId, out String? text) ? text : null;
		}

		/// <summary>Replaces the cached message texts of a script (after the message editor saved the file).</summary>
		public void UpdateMessageTexts(DocumentViewModel document, MessageDocument messages)
		{
			ArgumentNullException.ThrowIfNull(document);
			ArgumentNullException.ThrowIfNull(messages);
			this.Store(document, messages);
		}

		/// <summary>
		/// Reads the associated message file of a script again and replaces the cached texts (legacy: done whenever the script tab is
		/// selected). The text of the message file is taken from its tab when it is open, so unsaved edits count.
		/// </summary>
		/// <param name="document">The script.</param>
		/// <param name="openTexts">The texts of the open documents by full path.</param>
		/// <returns>A task that completes when the texts are replaced; the old texts stay when the file cannot be read.</returns>
		public async Task RefreshAsync(DocumentViewModel document, IReadOnlyDictionary<String, String> openTexts)
		{
			ArgumentNullException.ThrowIfNull(document);
			ArgumentNullException.ThrowIfNull(openTexts);

			String? path = document.MessageFilePath;
			if (path is null)
			{
				return;
			}

			if (!openTexts.TryGetValue(DocumentPaths.GetFullPath(path), out String? text) && !File.Exists(path))
			{
				return;
			}

			try
			{
				MessageDocument messages = text is not null
					? MessageDocument.Parse(text.Split('\n').Select(line => line.TrimEnd('\r')).ToList())
					: await this._messageFiles.ReadAsync(path, this._settings.Effective.ToMessageFileOptions().Encoding);
				this.Store(document, messages);
				this._problems.ClearOwner(document, ProblemSource.Message);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.MessageFileNotRead(ex, path);
				this._problems.Report(ProblemFactory.OpenFailed(path, ex) with { Source = ProblemSource.Message, Owner = document });
			}
		}

		/// <summary>Drops the cached message texts of a closed document.</summary>
		public void Forget(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			this._forgotten.AddOrUpdate(document, document);
			this._messageTexts.Remove(document);
		}

		/// <summary>Remembers the texts of a script, unless the script was closed while its message file was being read.</summary>
		private void Store(DocumentViewModel document, MessageDocument messages)
		{
			if (!this._forgotten.TryGetValue(document, out _))
			{
				this._messageTexts[document] = messages.BuildTextMap();
			}
		}

		/// <summary>Opens the message editor for the message file of a script (asking to create the file when it does not exist).</summary>
		/// <param name="workspace">The workspace that receives the lines sent back by the message editor.</param>
		/// <param name="document">The script.</param>
		public async Task OpenEditorAsync(IWorkspaceManager workspace, DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);
			if (document.FilePath is null)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Dlg.MessageFileTitle"), Loc.Get("Str.Shell.Dlg.SaveScriptFirst"));
				return;
			}

			if (document.Kind != DocumentKind.Script)
			{
				await this._dialogs.ShowInfoAsync(document.FileName, Loc.Get("Str.Shell.Dlg.NoMessageFileKind"));
				return;
			}

			Boolean opened = await this._messageEditor.OpenForScriptAsync(document.FilePath, document.ProgramInfo?.ScriptNameId, new DocumentMessageTarget(workspace, document), true);
			if (opened)
			{
				await this.AssociateAsync(document, false, false);
			}
		}

		/// <summary>Finds (and optionally creates) the message file of a script, remembers it in the document and loads its texts.</summary>
		/// <returns>The path of the message file, or <see langword="null"/>.</returns>
		public async Task<String?> AssociateAsync(DocumentViewModel document, Boolean create, Boolean reportProblems)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (document.FilePath is null || document.Kind != DocumentKind.Script)
			{
				if (reportProblems)
				{
					await this._dialogs.ShowInfoAsync(document.FileName, Loc.Get("Str.Shell.Dlg.NoMessageFileKind"));
				}

				return null;
			}

			MessageFileOptions options = this._settings.Effective.ToMessageFileOptions();
			MessageFileLocation location = this._messageFiles.LocateForScript(document.FilePath, document.ProgramInfo?.ScriptNameId, options);
			if (reportProblems)
			{
				await this.ReportLookupIssuesAsync(location.Issues);
			}

			String? path = location.Found ? location.Path : null;
			if (path is null && create && location.DefaultPath is not null)
			{
				Boolean confirmed = await this._dialogs.ConfirmAsync(
					Loc.Get("Str.Shell.Dlg.MessageFileTitle"),
					Loc.Format("Str.Shell.Dlg.MessageFileCreate", location.FileName, location.DefaultDirectory));
				if (confirmed)
				{
					try
					{
						path = await this._messageFiles.CreateFileAsync(location);
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						this._logger.MessageFileNotCreated(ex, location.DefaultPath);
						this._problems.Report(ProblemFactory.SaveFailed(location.DefaultPath, ex, document) with { Source = ProblemSource.Message });
						await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.MessageFileTitle"), Loc.Get("Str.MessageEditor.Msg.CreateFailed") + Environment.NewLine + ex.Message);
					}
				}
			}

			if (path is null)
			{
				if (reportProblems)
				{
					this._problems.Report(ProblemFactory.MessageFileMissing(document.FilePath, document));
				}

				return null;
			}

			this._problems.ClearOwner(document, ProblemSource.Message);
			document.MessageFilePath = path;
			try
			{
				MessageDocument messages = await this._messageFiles.ReadAsync(path, options.Encoding);
				this.Store(document, messages);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.MessageFileNotRead(ex, path);
				this._problems.Report(ProblemFactory.OpenFailed(path, ex) with { Source = ProblemSource.Message, Owner = document });
			}

			return path;
		}

		private async Task ReportLookupIssuesAsync(MessageLookupIssues issues)
		{
			if ((issues & MessageLookupIssues.ScriptsListMissing) != 0)
			{
				await this._dialogs.ShowWarningAsync("scripts.lst", Loc.Get("Str.Shell.Dlg.ScriptsLstMissing"));
			}

			if ((issues & MessageLookupIssues.ScriptsListEntryInvalid) != 0)
			{
				await this._dialogs.ShowWarningAsync("scripts.lst", Loc.Get("Str.Shell.Dlg.ScriptsLstInvalid"));
			}

			if ((issues & MessageLookupIssues.ScriptIdNotFound) != 0)
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Shell.Dlg.MessageFileTitle"), Loc.Get("Str.Shell.Dlg.NameMacroNotFound"));
			}
		}
	}
}
