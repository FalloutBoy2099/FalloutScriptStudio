namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// Remembers, per project, which documents were open (and which one was active) and opens them again later. The list is kept in the
	/// persisted <see cref="RecentFiles"/> (paths relative to the project folder); the caret line and the folded procedures of each document
	/// are already remembered per script and come back by themselves when the document is opened.
	/// </summary>
	public sealed class ProjectSessionService
	{
		private readonly IWorkspaceManager _workspace;
		private readonly ISettingsService _settings;
		private readonly IProjectService _projects;
		private readonly IProblemService _problems;
		private readonly ILogger? _logger;

		/// <summary>Initializes a new instance of the <see cref="ProjectSessionService"/> class.</summary>
		/// <param name="workspace">The open documents.</param>
		/// <param name="settings">The settings, which hold the remembered lists and the option.</param>
		/// <param name="projects">The projects.</param>
		/// <param name="problems">Receives the hint offered when documents are open already.</param>
		/// <param name="logger">The logger.</param>
		public ProjectSessionService(IWorkspaceManager workspace, ISettingsService settings, IProjectService projects, IProblemService problems, ILogger<ProjectSessionService>? logger = null)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(projects);
			ArgumentNullException.ThrowIfNull(problems);
			this._workspace = workspace;
			this._settings = settings;
			this._projects = projects;
			this._problems = problems;
			this._logger = logger;
		}

		/// <summary>
		/// Remembers the documents that are open now as the session of a project (call it when the project is left or the program ends).
		/// Documents outside the project folder and documents without a file are not remembered.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		public void Capture(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);

			IEnumerable<String> paths = this._workspace.Documents.Select(document => document.FilePath).OfType<String>();
			this._settings.Recent.SetProjectSession(ProjectSession.Create(projectFolder, paths, this._workspace.ActiveDocument?.FilePath));
		}

		/// <summary>Gets how many remembered documents of a project still exist.</summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <returns>The number of documents that could be opened again.</returns>
		public Int32 CountRestorable(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			return this._settings.Recent.GetProjectSession(projectFolder)?.GetDocumentPaths().Count(File.Exists) ?? 0;
		}

		/// <summary>
		/// A project was opened. With the option "restore the open files of a project" turned off nothing happens. Otherwise the remembered
		/// documents are opened at once when nothing is open, and offered with a hint (an information problem with a "Restore open files"
		/// link) when other documents are open, so that a switch of project never fills the tab bar unasked.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <returns>What was done.</returns>
		public async Task<ProjectSessionOffer> OfferAsync(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);

			_ = this._problems.ClearOrigin(ProblemOrigins.ProjectSession);
			if (!this._settings.Settings.Editor.RestoreProjectDocuments)
			{
				return new ProjectSessionOffer(ProjectSessionOfferKind.None, 0);
			}

			Int32 count = this.CountRestorable(projectFolder);
			if (count == 0)
			{
				return new ProjectSessionOffer(ProjectSessionOfferKind.None, 0);
			}

			if (this._workspace.Documents.Count == 0)
			{
				Int32 restored = await this.RestoreAsync(projectFolder).ConfigureAwait(true);
				return new ProjectSessionOffer(ProjectSessionOfferKind.Restored, restored);
			}

			this._problems.Report(ProblemFactory.ProjectDocumentsAvailable(projectFolder, this._projects.DisplayName, count));
			return new ProjectSessionOffer(ProjectSessionOfferKind.Offered, count);
		}

		/// <summary>
		/// Opens the remembered documents of a project again (in the background, the remembered active one last and in front). A file that no
		/// longer exists is skipped and only logged.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <returns>How many documents were opened.</returns>
		public async Task<Int32> RestoreAsync(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);

			_ = this._problems.ClearOrigin(ProblemOrigins.ProjectSession);
			ProjectSession? session = this._settings.Recent.GetProjectSession(projectFolder);
			if (session is null)
			{
				return 0;
			}

			String? activePath = session.GetActiveDocumentPath();
			DocumentViewModel? active = null;
			DocumentViewModel? last = null;
			Int32 opened = 0;
			foreach (String path in session.GetDocumentPaths())
			{
				if (!File.Exists(path))
				{
					this._logger?.RememberedDocumentSkipped(path);
					continue;
				}

				DocumentViewModel? document = await this._workspace.OpenAsync(path, new OpenDocumentOptions { Activate = false, AddToRecent = false, ClearBuildLog = false }).ConfigureAwait(true);
				if (document is null)
				{
					continue;
				}

				opened++;
				last = document;
				if (activePath is not null && String.Equals(path, activePath, StringComparison.OrdinalIgnoreCase))
				{
					active = document;
				}
			}

			DocumentViewModel? front = active ?? last;
			if (front is not null)
			{
				this._workspace.ActiveDocument = front;
			}

			return opened;
		}
	}
}
