namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Microsoft.Extensions.DependencyInjection;
	using ViewModels.ScriptTools;
	using Views.ScriptTools;

	/// <summary>Shows the preview of "Replace in Files" in a modal window.</summary>
	public sealed class ReplacePreviewPresenter : IReplacePreviewPresenter
	{
		private readonly IServiceProvider _services;

		/// <summary>Initializes a new instance of the <see cref="ReplacePreviewPresenter"/> class.</summary>
		/// <param name="services">Creates the window and its view model.</param>
		public ReplacePreviewPresenter(IServiceProvider services)
		{
			ArgumentNullException.ThrowIfNull(services);
			this._services = services;
		}

		/// <inheritdoc />
		public Task<IReadOnlyList<ReplaceSelection>?> ShowAsync(ReplacePlan plan, ReplaceInFilesRequest request)
		{
			ArgumentNullException.ThrowIfNull(plan);
			ArgumentNullException.ThrowIfNull(request);

			ReplacePreviewViewModel viewModel = this._services.GetRequiredService<ReplacePreviewViewModel>();
			viewModel.Initialize(plan, request);
			ReplacePreviewWindow window = ActivatorUtilities.CreateInstance<ReplacePreviewWindow>(this._services, viewModel);
			window.Owner = ScriptToolWindowHelper.FindOwner(window);
			window.ShowDialog();
			return Task.FromResult(viewModel.Selection);
		}
	}
}