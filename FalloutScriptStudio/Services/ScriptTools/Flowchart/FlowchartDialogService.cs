namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Threading.Tasks;
	using Microsoft.Extensions.DependencyInjection;
	using Documents;
	using Views.Flowchart;
	using Views.ScriptTools;

	/// <inheritdoc />
	public sealed class FlowchartDialogService : IFlowchartDialogService
	{
		private readonly IServiceProvider _services;

		/// <summary>
		/// Initializes a new instance of the <see cref="FlowchartDialogService"/> class.
		/// </summary>
		/// <param name="services">Creates the dialogs.</param>
		public FlowchartDialogService(IServiceProvider services)
		{
			ArgumentNullException.ThrowIfNull(services);
			this._services = services;
		}

		/// <inheritdoc />
		public Task<String?> ShowNewNodeAsync(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);

			NewNodeWindow window = this._services.GetRequiredService<NewNodeWindow>();
			window.ViewModel.Initialize(script);
			window.Owner = ScriptToolWindowHelper.FindOwner(window);
			window.ShowDialog();
			return Task.FromResult(window.ViewModel.CreatedName);
		}

		/// <inheritdoc />
		public Task<String?> EditMessageTextAsync(String caption, String text)
		{
			ArgumentNullException.ThrowIfNull(caption);
			ArgumentNullException.ThrowIfNull(text);

			MessageTextWindow window = this._services.GetRequiredService<MessageTextWindow>();
			window.ViewModel.Initialize(caption, text);
			window.Owner = ScriptToolWindowHelper.FindOwner(window);
			window.ShowDialog();
			return Task.FromResult(window.ViewModel.Result);
		}
	}
}