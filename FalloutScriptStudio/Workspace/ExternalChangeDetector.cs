namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Documents;
	using Localization;
	using Services;

	/// <summary>Detects documents whose file was changed by another program and offers to reload them.</summary>
	public sealed class ExternalChangeDetector
	{
		private readonly IDialogService _dialogs;
		private Boolean _checking;

		/// <summary>Initializes the detector.</summary>
		public ExternalChangeDetector(IDialogService dialogs)
		{
			ArgumentNullException.ThrowIfNull(dialogs);
			this._dialogs = dialogs;
		}

		/// <summary>
		/// Compares the time stamp of the file of every document with the one stored in the document and asks whether the
		/// changed ones should be reloaded. A check that is already running is not started twice.
		/// </summary>
		/// <param name="documents">The open documents.</param>
		/// <param name="reloadAsync">Reloads a document from disk.</param>
		public async Task CheckAsync(IEnumerable<DocumentViewModel> documents, Func<DocumentViewModel, Task<Boolean>> reloadAsync)
		{
			ArgumentNullException.ThrowIfNull(documents);
			ArgumentNullException.ThrowIfNull(reloadAsync);
			if (this._checking)
			{
				return;
			}

			this._checking = true;
			try
			{
				foreach (DocumentViewModel document in documents.ToList())
				{
					if (document.FilePath is null || !File.Exists(document.FilePath))
					{
						continue;
					}

					DateTime stamp = File.GetLastWriteTimeUtc(document.FilePath);
					if (stamp == document.LastWriteTimeUtc)
					{
						continue;
					}

					document.LastWriteTimeUtc = stamp;
					String text = Loc.Format(document.IsModified ? "Str.Shell.Dlg.FileChangedModified" : "Str.Shell.Dlg.FileChanged", document.FilePath);
					if (await this._dialogs.ConfirmAsync(Loc.Get("Str.Shell.Dlg.FileChangedTitle"), text))
					{
						await reloadAsync(document);
					}
				}
			}
			finally
			{
				this._checking = false;
			}
		}
	}
}