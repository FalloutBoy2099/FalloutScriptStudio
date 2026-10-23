namespace FalloutScriptStudio.ViewModels.Editor
{
	using System;
	using System.Windows.Media;

	/// <summary>One block of a tooltip: a colored title line and an optional description.</summary>
	public sealed class TipSectionViewModel
	{
		public TipSectionViewModel(String title, String body, Brush titleBrush, String hint = "")
		{
			this.Title = title;
			this.Body = body;
			this.TitleBrush = titleBrush;
			this.Hint = hint;
		}

		public String Title { get; }

		public String Body { get; }

		public Brush TitleBrush { get; }

		/// <summary>Gets the small dimmed line under the description, or an empty text.</summary>
		public String Hint { get; }

		public Boolean HasBody => this.Body.Length > 0;

		public Boolean HasHint => this.Hint.Length > 0;
	}
}