namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	/// <summary>Opening of dropped files.</summary>
	public sealed partial class WorkspaceService
	{
		/// <summary>
		/// Opens dropped files the way the legacy editor did: they go to the recent files list and a file that is already open is
		/// offered as a copy. The main window and the text of an editor share this path.
		/// </summary>
		/// <param name="filePaths">The dropped files.</param>
		/// <returns>A task that completes when all files were handled.</returns>
		public async Task OpenDroppedFilesAsync(IReadOnlyList<String> filePaths)
		{
			ArgumentNullException.ThrowIfNull(filePaths);
			foreach (String filePath in filePaths)
			{
				await this.OpenAsync(filePath, new OpenDocumentOptions { PromptIfAlreadyOpen = true });
			}
		}
	}
}