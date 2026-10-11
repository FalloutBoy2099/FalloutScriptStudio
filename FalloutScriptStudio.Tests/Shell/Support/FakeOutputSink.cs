namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Workspace;

	/// <summary>An output sink that records what the workspace sent to it.</summary>
	internal sealed class FakeOutputSink : IOutputSink
	{
		public List<BuildError> SearchResults { get; } = new List<BuildError>();

		public String? SearchTitle { get; private set; }

		public List<String> Statuses { get; } = new List<String>();

		public List<String> BuildLines { get; } = new List<String>();

		public List<OutputTab> Tabs { get; } = new List<OutputTab>();

		public Int32 ClearedErrors { get; private set; }

		public List<BuildError> AggregatedErrors { get; } = new List<BuildError>();

		public void ShowSearchResults(IReadOnlyList<BuildError> results, String titleKey, String? titleArgument = null)
		{
			this.SearchResults.Clear();
			this.SearchResults.AddRange(results);
			this.SearchTitle = titleArgument is null ? Loc.Get(titleKey) : Loc.Format(titleKey, titleArgument);
		}

		public void ShowBuildErrors(IReadOnlyList<BuildError> errors)
		{
			this.AggregatedErrors.Clear();
			this.AggregatedErrors.AddRange(errors);
		}

		public void ReportStatus(String message)
		{
			this.Statuses.Add(message);
		}

		public void SetBuildText(String text)
		{
			this.BuildLines.Clear();
			if (text.Length > 0)
			{
				this.BuildLines.Add(text);
			}
		}

		public void AppendBuildLine(String line)
		{
			this.BuildLines.Add(line);
		}

		public void ShowTab(OutputTab tab)
		{
			this.Tabs.Add(tab);
		}

		public void ClearErrors()
		{
			this.ClearedErrors++;
		}
	}
}
