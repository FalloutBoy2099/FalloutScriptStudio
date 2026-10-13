namespace FalloutScriptStudio.ViewModels.Main
{
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;

	/// <summary>The commands that use the index of the symbols of the project.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Opens the list of the procedures, macros and variables of the whole project (Ctrl+T).</summary>
		/// <returns>A task that completes when the list was closed and the symbol opened.</returns>
		[RelayCommand]
		public Task GoToSymbolAsync()
		{
			return this._workspace.GuardAsync(() => this._workspace.GoToSymbolAsync());
		}
	}
}
