namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// View model of the About window.
	/// </summary>
	public sealed partial class AboutViewModel : ToolWindowViewModelBase
	{
		/// <summary>The name of the application.</summary>
		public const String ApplicationName = "Fallout Script Studio";

		/// <summary>The author of this rewrite.</summary>
		public const String Author = "Kain";

		/// <summary>The license notice shown in the window (the standard GNU GPL version 3 notice).</summary>
		public const String LicenseNotice =
			"This program is free software: you can redistribute it and/or modify it under the terms of the "
			+ "GNU General Public License as published by the Free Software Foundation, either version 3 of the License, "
			+ "or (at your option) any later version.\n\n"
			+ "This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; "
			+ "without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. "
			+ "See the GNU General Public License for more details.\n\n"
			+ "You should have received a copy of the GNU General Public License along with this program. "
			+ "If not, see <https://www.gnu.org/licenses/>.";

		/// <summary>
		/// Initializes a new instance of the <see cref="AboutViewModel"/> class for the running application.
		/// </summary>
		public AboutViewModel()
		{
			Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(AboutViewModel).Assembly;
			String? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
			this.Version = FormatVersion(informational ?? assembly.GetName().Version?.ToString());
		}

		/// <summary>Gets the name of the application.</summary>
		public String ProductName => ApplicationName;

		/// <summary>Gets the author line, for example <c>by Kain</c>.</summary>
		public String AuthorLine => Loc.Format("Str.ToolWindows.About.AuthorLine", Author);

		/// <summary>Gets the version line, for example <c>Version 1.0.0</c>.</summary>
		public String Version { get; }

		/// <summary>Gets the copyright line.</summary>
		public String Copyright => Loc.Format("Str.ToolWindows.About.Copyright", Author);

		/// <summary>Gets the short description of the application.</summary>
		public String Description => Loc.Format("Str.ToolWindows.About.Description", Author);

		/// <summary>Gets the notice about the original editor and its authors.</summary>
		public String OriginalWork => Loc.Get("Str.ToolWindows.About.OriginalWork");

		/// <summary>Gets the license notice.</summary>
		public String License => LicenseNotice;

		/// <summary>Gets the third-party components and original works that are credited.</summary>
		public IReadOnlyList<AboutCredit> Credits { get; } = new List<AboutCredit>
		{
			new AboutCredit("Sfall Script Editor", Loc.Get("Str.ToolWindows.About.Credit.SfallScriptEditor")),
			new AboutCredit("sslc, sfall edition", Loc.Get("Str.ToolWindows.About.Credit.Sslc")),
			new AboutCredit("mcpp 2.7.2 (BCD)", Loc.Get("Str.ToolWindows.About.Credit.Mcpp")),
			new AboutCredit("Open Watcom C32 2.0 beta (01/11/2017)", Loc.Get("Str.ToolWindows.About.Credit.Watcom")),
			new AboutCredit("int2ssl 8.4.6", Loc.Get("Str.ToolWindows.About.Credit.Int2ssl")),
			new AboutCredit("AvalonEdit", Loc.Get("Str.ToolWindows.About.Credit.AvalonEdit")),
			new AboutCredit("WPF-UI", Loc.Get("Str.ToolWindows.About.Credit.WpfUi")),
			new AboutCredit("CommunityToolkit.Mvvm", Loc.Get("Str.ToolWindows.About.Credit.Mvvm")),
		};

		/// <summary>Gets the web links.</summary>
		public IReadOnlyList<AboutLink> Links { get; } = new List<AboutLink>
		{
			new AboutLink("GNU General Public License v3", new Uri("https://www.gnu.org/licenses/gpl-3.0.html")),
			new AboutLink("sfall", new Uri("https://github.com/sfall-team/sfall")),
		};

		/// <summary>
		/// Formats the version for display: cuts the build metadata (<c>+commit</c>) and adds the "Version" caption.
		/// </summary>
		/// <param name="rawVersion">The version text, may be <see langword="null"/>.</param>
		/// <returns>The text to show.</returns>
		public static String FormatVersion(String? rawVersion)
		{
			String version = (rawVersion ?? String.Empty).Trim();
			Int32 plus = version.IndexOf('+', StringComparison.Ordinal);
			if (plus >= 0)
			{
				version = version.Substring(0, plus);
			}

			return version.Length == 0 ? Loc.Get("Str.ToolWindows.About.VersionUnknown") : Loc.Format("Str.ToolWindows.About.Version", version);
		}

		[RelayCommand]
		private void Close()
		{
			this.RequestClose(true);
		}
	}
}
