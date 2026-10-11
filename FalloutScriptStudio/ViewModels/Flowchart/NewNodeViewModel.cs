namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.ViewModels.ToolWindows;

	/// <summary>
	/// The view model of the "new dialogue node" dialog (legacy <c>TemplateNode</c>): the name of the node and its starting code.
	/// </summary>
	public sealed partial class NewNodeViewModel : ToolWindowViewModelBase
	{
		private readonly DialogueScriptEditor _scriptEditor;
		private readonly IDialogService _dialogs;
		private DocumentViewModel? _script;

		/// <summary>
		/// Initializes a new instance of the <see cref="NewNodeViewModel"/> class.
		/// </summary>
		/// <param name="scriptEditor">Adds the node to the script.</param>
		/// <param name="dialogs">Shows messages.</param>
		public NewNodeViewModel(DialogueScriptEditor scriptEditor, IDialogService dialogs)
		{
			ArgumentNullException.ThrowIfNull(scriptEditor);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._scriptEditor = scriptEditor;
			this._dialogs = dialogs;
			this.SelectedTemplate = this.Templates[0];
		}

		/// <summary>Gets the code templates.</summary>
		public IReadOnlyList<DialogueNodeTemplate> Templates { get; } = DialogueNodeTemplate.All;

		/// <summary>Gets the name of the node that was created, or <see langword="null"/> when the dialog was cancelled.</summary>
		public String? CreatedName { get; private set; }

		/// <summary>Gets or sets the name of the new node.</summary>
		[ObservableProperty]
		public partial String NodeName { get; set; } = DialogueNodeTemplate.DefaultNodeName;

		/// <summary>Gets or sets the code of the new node.</summary>
		[ObservableProperty]
		public partial String Code { get; set; } = String.Empty;

		/// <summary>Gets or sets the chosen template.</summary>
		[ObservableProperty]
		public partial DialogueNodeTemplate SelectedTemplate { get; set; }

		/// <summary>Sets the script that receives the node.</summary>
		/// <param name="script">The script.</param>
		public void Initialize(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);
			this._script = script;
		}

		partial void OnSelectedTemplateChanged(DialogueNodeTemplate value)
		{
			this.Code = value.Code;
		}

		/// <summary>Creates the node in the script; the dialog closes on success and stays open when the name is refused.</summary>
		/// <returns>A task that completes when the node is created or refused.</returns>
		[RelayCommand]
		public async Task CreateAsync()
		{
			if (this._script is null)
			{
				return;
			}

			NodeCreationResult result = await this._scriptEditor.CreateNodeAsync(this._script, this.NodeName, this.Code);
			switch (result.Error)
			{
				case NodeCreationError.None:
					this.CreatedName = result.NodeName;
					this.RequestClose(true);
					break;
				case NodeCreationError.NameExists:
					await this._dialogs.ShowWarningAsync(Loc.Get("Str.Flowchart.Msg.UnableToCreate.Title"), Loc.Get("Str.Flowchart.Msg.NameExists"));
					break;
				default:
					await this._dialogs.ShowWarningAsync(Loc.Get("Str.Flowchart.Msg.UnableToCreate.Title"), Loc.Get("Str.Flowchart.Msg.NameInvalid"));
					break;
			}
		}

		/// <summary>Closes the dialog without creating a node.</summary>
		[RelayCommand]
		public void Cancel()
		{
			this.RequestClose(false);
		}
	}
}
