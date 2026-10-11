namespace FalloutScriptStudio.ViewModels.Editor
{
	using System;
	using System.Windows.Media;

	/// <summary>One block of a tooltip: a colored title line and an optional description.</summary>
	public sealed class TipSectionViewModel
	{
		public TipSectionViewModel(String title, String body, Brush titleBrush)
		{
			this.Title = title;
			this.Body = body;
			this.TitleBrush = titleBrush;
		}

		public String Title { get; }

		public String Body { get; }

		public Brush TitleBrush { get; }

		public Boolean HasBody => this.Body.Length > 0;
	}
}
