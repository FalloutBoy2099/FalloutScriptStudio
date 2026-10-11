namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>
	/// Copies the options the settings window owns from the edited copy into the live settings. Everything else (window layout,
	/// last used folders, the chosen preprocessor definition, search switches, the zoom… written by other components while or
	/// before the dialog was open) stays as it is, so a concurrent write is never lost and the objects that hold the live settings
	/// stay valid. An option added to the settings model is owned by the window unless it is listed in <see cref="ForeignOptions"/>.
	/// </summary>
	public static class SettingsDialogMerge
	{
		/// <summary>
		/// The options that are not part of the settings window, as <c>Section.Property</c> names; the other components write them.
		/// </summary>
		public static readonly IReadOnlySet<String> ForeignOptions = new HashSet<String>(StringComparer.Ordinal)
		{
			"Paths.LastMassCompileDirectory",
			"Paths.LastSearchDirectory",
			"Paths.SolutionFolder",
			"Compiler.PreprocessorDefine",
			"Editor.SearchIgnoreCase",
			"Editor.SearchWholeWord",
			"Editor.ZoomPercent",
			"Messages.FontSizeIndex",
			"Messages.RegisterScriptAllowDefine",
		};

		/// <summary>
		/// Copies the options shown in the settings window from <paramref name="edited"/> into <paramref name="live"/>.
		/// </summary>
		/// <param name="live">The current settings, modified in place.</param>
		/// <param name="edited">The copy the dialog edited.</param>
		public static void CopyOwnedOptions(AppSettings live, AppSettings edited)
		{
			ArgumentNullException.ThrowIfNull(live);
			ArgumentNullException.ThrowIfNull(edited);

			CopySection(live.Paths, edited.Paths, nameof(AppSettings.Paths));
			CopySection(live.Compiler, edited.Compiler, nameof(AppSettings.Compiler));
			CopySection(live.Editor, edited.Editor, nameof(AppSettings.Editor));
			CopySection(live.Messages, edited.Messages, nameof(AppSettings.Messages));
			CopySection(live.Flowchart, edited.Flowchart, nameof(AppSettings.Flowchart));
		}

		private static void CopySection<T>(T live, T edited, String section)
			where T : class
		{
			foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (!property.CanRead || !property.CanWrite || ForeignOptions.Contains(section + "." + property.Name))
				{
					continue;
				}

				Object? value = property.GetValue(edited);
				if (value is List<String> list)
				{
					value = new List<String>(list);
				}

				property.SetValue(live, value);
			}
		}
	}
}
