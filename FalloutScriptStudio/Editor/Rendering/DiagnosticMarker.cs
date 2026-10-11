namespace FalloutScriptStudio.Editor.Rendering
{
	using System;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>A wavy-underlined range of the text with a message (parser error, warning…).</summary>
	public sealed class DiagnosticMarker : TextSegment
	{
		public DiagnosticMarker(Int32 offset, Int32 length, MarkerSeverity severity, String message)
		{
			this.StartOffset = offset;
			this.Length = length;
			this.Severity = severity;
			this.Message = message;
		}

		public MarkerSeverity Severity { get; }

		public String Message { get; }
	}
}
