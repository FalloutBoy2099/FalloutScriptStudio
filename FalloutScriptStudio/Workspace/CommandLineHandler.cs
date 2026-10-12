namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ScriptTools;

	/// <summary>Opens the files passed on the command line, or forwarded by a second instance of the program.</summary>
	public sealed class CommandLineHandler
	{
		private readonly IWorkspaceManager _workspace;
		private readonly IMessageEditorService _messageEditor;
		private readonly IScriptToolService _scriptTools;
		private readonly IDialogService _dialogs;
		private readonly IProjectService? _projects;

		/// <summary>Initializes the handler.</summary>
		/// <param name="workspace">The workspace the documents are opened in.</param>
		/// <param name="messageEditor">Opens message files.</param>
		/// <param name="scriptTools">Opens flowcharts.</param>
		/// <param name="dialogs">Shows the messages.</param>
		/// <param name="projects">Opens a folder given on the command line as the project; without it a folder is ignored.</param>
		public CommandLineHandler(IWorkspaceManager workspace, IMessageEditorService messageEditor, IScriptToolService scriptTools, IDialogService dialogs, IProjectService? projects = null)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(messageEditor);
			ArgumentNullException.ThrowIfNull(scriptTools);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._workspace = workspace;
			this._messageEditor = messageEditor;
			this._scriptTools = scriptTools;
			this._dialogs = dialogs;
			this._projects = projects;
		}

		/// <summary>Opens each argument: scripts and headers in tabs, message files in the message editor, flowcharts through their script, a folder as the project.</summary>
		/// <returns><see langword="true"/> when at least one tab was opened or activated.</returns>
		public async Task<Boolean> HandleAsync(IEnumerable<String> arguments)
		{
			ArgumentNullException.ThrowIfNull(arguments);

			Boolean opened = false;
			foreach (String argument in arguments)
			{
				if (String.IsNullOrWhiteSpace(argument))
				{
					continue;
				}

				String path = Path.GetFullPath(argument);
				if (Directory.Exists(path))
				{
					// A folder is a project; the window is brought to the front like for a tab.
					opened |= this._projects?.Open(path) == true;
					continue;
				}

				String extension = Path.GetExtension(path).ToLowerInvariant();
				if (extension == ".msg")
				{
					DocumentViewModel? script = this._workspace.ActiveDocument is { Kind: DocumentKind.Script } active ? active : null;
					IMessageLineTarget? target = script is null ? null : this._workspace.CreateMessageTarget(script);
					this._messageEditor.Open(new MessageEditorOpenOptions { FilePath = path, Target = target, StartMaximized = true });
					continue;
				}

				Boolean flowchart = extension == ".fcd";
				if (flowchart)
				{
					String? script = FileAssociationService.ResolveFlowchartScript(path);
					if (script is null)
					{
						await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ScriptMissingTitle"), Loc.Get("Str.Shell.Dlg.FlowchartScriptMissing"));
						continue;
					}

					path = script;
				}

				DocumentViewModel? document = await this._workspace.OpenAsync(path, new OpenDocumentOptions { PromptIfAlreadyOpen = false });
				if (document is null)
				{
					continue;
				}

				opened = true;
				if (flowchart)
				{
					await this._scriptTools.ShowFlowchartAsync(document);
				}
			}

			return opened;
		}
	}
}
