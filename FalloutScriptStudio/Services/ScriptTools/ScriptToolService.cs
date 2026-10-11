namespace FalloutScriptStudio.Services.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Threading.Tasks;
	using System.Windows;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Services.ScriptTools.Registration;
	using FalloutScriptStudio.Views.DialoguePreview;
	using FalloutScriptStudio.Views.Flowchart;
	using FalloutScriptStudio.Views.ScriptTools;
	using Microsoft.Extensions.DependencyInjection;

	/// <inheritdoc />
	public sealed class ScriptToolService : IScriptToolService
	{
		private const Int32 MaxInitialSearchTextLength = 255;

		private readonly IServiceProvider _services;
		private readonly IDialogService _dialogs;

		private readonly HashSet<DocumentViewModel> _previewsOpening = new HashSet<DocumentViewModel>();
		private RegisterScriptWindow? _registerWindow;
		private Boolean _registerOpening;
		private AdvancedSearchWindow? _searchWindow;
		private readonly Dictionary<DocumentViewModel, FlowchartWindow> _flowcharts = new Dictionary<DocumentViewModel, FlowchartWindow>();
		private readonly Dictionary<DocumentViewModel, DialoguePreviewWindow> _previews = new Dictionary<DocumentViewModel, DialoguePreviewWindow>();
		private String _lastSearchText = String.Empty;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptToolService"/> class.
		/// </summary>
		/// <param name="services">Creates the transient tool windows (resolved lazily so the workspace may depend on this service).</param>
		/// <param name="dialogs">Shows messages.</param>
		public ScriptToolService(IServiceProvider services, IDialogService dialogs)
		{
			ArgumentNullException.ThrowIfNull(services);
			ArgumentNullException.ThrowIfNull(dialogs);

			this._services = services;
			this._dialogs = dialogs;
		}

		/// <inheritdoc />
		public async Task<String?> PromptProcedureNameAsync(String initialName, ProcedureNameDialogMode mode, IReadOnlyCollection<String> existingNames)
		{
			ProcedureNameResult? result = await this.PromptProcedureAsync(new ProcedureNameRequest
			{
				InitialName = initialName,
				Mode = mode,
				ExistingNames = existingNames,
			});
			return result?.Declaration;
		}

		/// <inheritdoc />
		public Task<ProcedureNameResult?> PromptProcedureAsync(ProcedureNameRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			ProcedureNameWindow window = this._services.GetRequiredService<ProcedureNameWindow>();
			window.ViewModel.Initialize(request);
			window.Owner = ScriptToolWindowHelper.FindOwner(window);
			window.ShowDialog();
			return Task.FromResult(window.ViewModel.Result);
		}

		/// <inheritdoc />
		public async Task ShowRegisterScriptAsync(DocumentViewModel? script)
		{
			if (this._registerOpening)
			{
				// The window is still being prepared (a double click): a second one would open next to it.
				return;
			}

			this._registerOpening = true;
			try
			{
				await this.OpenRegisterScriptAsync(script);
			}
			finally
			{
				this._registerOpening = false;
			}
		}

		private async Task OpenRegisterScriptAsync(DocumentViewModel? script)
		{
			String? scriptFileName = null;
			if (script is not null)
			{
				ScriptRegistrationCheck check = ScriptRegistrationChecker.Check(script.FilePath);
				if (check.Kind == ScriptRegistrationCheckKind.Refused)
				{
					await this._dialogs.ShowErrorAsync(check.Title, check.Message);
					return;
				}

				if (check.Kind == ScriptRegistrationCheckKind.NeedsConfirmation && !await this._dialogs.ConfirmAsync(check.Title, check.Message, Loc.Get("Str.ScriptTools.ToolsContinue"), Loc.Get("Str.Common.Cancel")))
				{
					return;
				}

				scriptFileName = check.ScriptFileName;
			}

			if (this._registerWindow is not null)
			{
				if (scriptFileName is not null)
				{
					this._registerWindow.ViewModel.RegisterScript(scriptFileName);
				}

				Activate(this._registerWindow);
				return;
			}

			RegisterScriptWindow window = this._services.GetRequiredService<RegisterScriptWindow>();
			if (!await window.ViewModel.InitializeAsync(scriptFileName))
			{
				return;
			}

			window.Owner = ScriptToolWindowHelper.FindOwner(window);
			window.Closed += (_, _) => this._registerWindow = null;
			this._registerWindow = window;
			window.Show();
		}

		/// <inheritdoc />
		public Task ShowAdvancedSearchAsync(String? initialText)
		{
			AdvancedSearchWindow? window = this._searchWindow;
			String text = this.ChooseSearchText(initialText, window?.ViewModel.SearchText);
			if (window is null)
			{
				window = this._services.GetRequiredService<AdvancedSearchWindow>();
				window.Owner = ScriptToolWindowHelper.FindOwner(window);
				AdvancedSearchWindow created = window;
				window.Closed += (_, _) =>
				{
					this._lastSearchText = created.ViewModel.SearchText;
					this._searchWindow = null;
				};
				this._searchWindow = window;
				window.ViewModel.SetSearchText(text);
				window.Show();
			}
			else
			{
				window.ViewModel.SetSearchText(text);
				Activate(window);
			}

			window.FocusSearchText();
			return Task.CompletedTask;
		}

		/// <inheritdoc />
		public async Task ShowFlowchartAsync(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);

			if (script.FilePath is null)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.ScriptTools.ToolsFlowchartTitle"), Loc.Get("Str.ScriptTools.ToolsFlowchartSaveFirst"));
				return;
			}

			if (this._flowcharts.TryGetValue(script, out FlowchartWindow? existing))
			{
				Activate(existing);
				return;
			}

			FlowchartWindow window = this._services.GetRequiredService<FlowchartWindow>();
			window.Initialize(script);
			window.Closed += (_, _) => this._flowcharts.Remove(script);
			this._flowcharts[script] = window;
			window.Show();
		}

		/// <inheritdoc />
		public async Task ShowDialoguePreviewAsync(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);

			if (this._previews.TryGetValue(script, out DialoguePreviewWindow? existing))
			{
				Activate(existing);
				return;
			}

			if (!this._previewsOpening.Add(script))
			{
				// The preview of this script is still being prepared (a double click): a second window would open next to it.
				return;
			}

			try
			{
				DialoguePreviewWindow window = this._services.GetRequiredService<DialoguePreviewWindow>();
				if (!await window.ViewModel.InitializeAsync(script))
				{
					window.ViewModel.Dispose();
					await this._dialogs.ShowInfoAsync(Loc.Get("Str.ScriptTools.ToolsPreviewTitle"), Loc.Get("Str.ScriptTools.ToolsPreviewNoDialogue"));
					return;
				}

				window.Owner = ScriptToolWindowHelper.FindOwner(window);
				window.Closed += (_, _) => this._previews.Remove(script);
				this._previews[script] = window;
				window.Show();
			}
			finally
			{
				this._previewsOpening.Remove(script);
			}
		}

		/// <inheritdoc />
		public Task ShowFunctionRulesAsync()
		{
			return this._services.GetRequiredService<IDialogueDialogsService>().ShowFunctionRulesAsync();
		}

		/// <inheritdoc />
		public async Task CloseToolWindowsAsync(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);

			INodeCodeEditorService? nodeEditors = this._services.GetService<INodeCodeEditorService>();
			if (nodeEditors is not null)
			{
				await nodeEditors.CloseEditorsAsync(script);
			}

			if (this._flowcharts.TryGetValue(script, out FlowchartWindow? flowchart))
			{
				flowchart.Close();
			}

			if (this._previews.TryGetValue(script, out DialoguePreviewWindow? preview))
			{
				preview.Close();
			}
		}

		private static void Activate(Window window)
		{
			if (window.WindowState == WindowState.Minimized)
			{
				window.WindowState = WindowState.Normal;
			}

			window.Activate();
		}

		/// <summary>
		/// Decides what the search field contains when the window is opened: the text the caller gave, otherwise the selection of
		/// the active editor, the text of an already open window, the text of the previous search, and finally the clipboard.
		/// </summary>
		private String ChooseSearchText(String? initialText, String? currentText)
		{
			String? selection = this._services.GetService<IWorkspace>()?.ActiveDocument?.Editor?.SelectedText;
			foreach (String? candidate in new[] { initialText, selection, currentText, this._lastSearchText })
			{
				if (!String.IsNullOrWhiteSpace(candidate))
				{
					return candidate.Trim().Length <= MaxInitialSearchTextLength ? candidate.Trim() : String.Empty;
				}
			}

			String clipboard = ReadClipboardText().Trim();
			return clipboard.Length is > 0 and < MaxInitialSearchTextLength ? clipboard : String.Empty;
		}

		private static String ReadClipboardText()
		{
			try
			{
				return Clipboard.ContainsText() ? Clipboard.GetText() : String.Empty;
			}
			catch (ExternalException)
			{
				return String.Empty;
			}
		}
	}
}
