namespace FalloutScriptStudio.Services.ScriptTools.Dialogs
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;
	using FalloutScriptStudio.Views.ScriptTools.Dialogs;
	using Microsoft.Extensions.DependencyInjection;

	/// <inheritdoc />
	public sealed class DialogueDialogsService : IDialogueDialogsService
	{
		private readonly IServiceProvider _services;
		private Boolean _addToHighlight;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueDialogsService"/> class.
		/// </summary>
		/// <param name="services">Creates the windows and their view models.</param>
		public DialogueDialogsService(IServiceProvider services)
		{
			ArgumentNullException.ThrowIfNull(services);
			this._services = services;
		}

		/// <inheritdoc />
		public async Task ShowFunctionRulesAsync()
		{
			DialogueRulesViewModel viewModel = this._services.GetRequiredService<DialogueRulesViewModel>();
			await viewModel.LoadAsync().ConfigureAwait(true);
			DialogueRulesWindow window = ActivatorUtilities.CreateInstance<DialogueRulesWindow>(this._services, viewModel);
			ShowModal(window);
		}

		/// <inheritdoc />
		public async Task<FunctionDefinition?> EditFunctionAsync(FunctionDefinition? existing)
		{
			FunctionEditResult? result = await this.ShowFunctionEditorAsync(existing).ConfigureAwait(true);
			return result?.Definition;
		}

		/// <inheritdoc />
		/// <remarks>The highlight choice is remembered for the next time a function is added.</remarks>
		public Task<FunctionEditResult?> ShowFunctionEditorAsync(FunctionDefinition? existing)
		{
			UserFunctionViewModel viewModel = this._services.GetRequiredService<UserFunctionViewModel>();
			viewModel.Initialize(existing, this._addToHighlight);
			UserFunctionWindow window = ActivatorUtilities.CreateInstance<UserFunctionWindow>(this._services, viewModel);
			ShowModal(window);

			if (viewModel.Result is null)
			{
				return Task.FromResult<FunctionEditResult?>(null);
			}

			if (viewModel.IsAdding)
			{
				this._addToHighlight = viewModel.AddToHighlight;
			}

			return Task.FromResult<FunctionEditResult?>(new FunctionEditResult(viewModel.Result, viewModel.IsAdding && viewModel.AddToHighlight && viewModel.CanHighlight));
		}

		/// <inheritdoc />
		public Task<IReadOnlyList<PreviewMatch>?> ConfirmMacroRenameAsync(MacroRenamePreview preview)
		{
			ArgumentNullException.ThrowIfNull(preview);

			MacroRenamePreviewViewModel viewModel = this._services.GetRequiredService<MacroRenamePreviewViewModel>();
			viewModel.Initialize(preview);
			MacroRenamePreviewWindow window = ActivatorUtilities.CreateInstance<MacroRenamePreviewWindow>(this._services, viewModel);
			ShowModal(window);
			return Task.FromResult(viewModel.AcceptedMatches);
		}

		private static Window? FindOwner(Window window)
		{
			Application? application = Application.Current;
			if (application is null)
			{
				return null;
			}

			Window? owner = application.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive && candidate.IsVisible && !ReferenceEquals(candidate, window));
			owner ??= application.MainWindow;
			return owner is not null && !ReferenceEquals(owner, window) && owner.IsVisible ? owner : null;
		}

		private static void ShowModal(Window window)
		{
			Window? owner = FindOwner(window);
			if (owner is not null)
			{
				window.Owner = owner;
				window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			else
			{
				window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}

			window.ShowDialog();
		}
	}
}
