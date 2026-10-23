namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.ViewModels.Panels;

	/// <summary>The commands of the Globals tab: showing it, and listing the findings about the global variables of the project.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>The index of the Globals tab in the right browser (the tabs are Procedures, Variables, Functions, Solution, Globals, Help).</summary>
		public const Int32 GlobalsTabIndex = 4;

		/// <summary>The index of the Help tab in the right browser.</summary>
		public const Int32 HelpTabIndex = 5;

		/// <summary>Gets the Globals panel: the global variables of the project.</summary>
		public GlobalsPanelViewModel? Globals { get; }

		/// <summary>Gets the Help panel: the documentation of the function under the caret.</summary>
		public HelpPanelViewModel? Help { get; }

		/// <summary>Shows the right browser on its Globals tab (View &gt; Show Global Variables).</summary>
		[RelayCommand]
		public void ShowGlobalVariables()
		{
			this.ShowRightBrowser = true;
			this.RightTabIndex = GlobalsTabIndex;
		}

		/// <summary>Shows the right browser on its Help tab (View &gt; Show Help Panel).</summary>
		[RelayCommand]
		public void ShowHelpPanel()
		{
			this.ShowRightBrowser = true;
			this.RightTabIndex = HelpTabIndex;
			this.Help?.Update();
		}

		/// <summary>Lists the findings about the global variables of the project in the problems (Code &gt; Check Global Variables) and shows the tab.</summary>
		/// <returns>A task that completes when the findings are listed.</returns>
		[RelayCommand]
		public Task CheckGlobalVariablesAsync()
		{
			if (this.Globals is not { } panel)
			{
				return Task.CompletedTask;
			}

			this.ShowGlobalVariables();
			return this._workspace.GuardAsync(async () =>
			{
				await panel.CheckAsync();
				if (panel.FindingCount > 0)
				{
					this.ShowProblems();
				}
			});
		}
	}
}