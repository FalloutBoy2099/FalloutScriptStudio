namespace FalloutScriptStudio.Core.Formatting
{
	using System;

	/// <summary>
	/// The fixed script the settings window formats with the current options, so that the user sees what each option does. It is written in
	/// a loose style on purpose: the spacing, the place of <c>begin</c> and <c>else</c>, the blank lines, the indentation and a long call are
	/// all things an option can change.
	/// </summary>
	public static class FormatPreviewSample
	{
		private static readonly String[] _lines =
		{
			"/* Sample script: the comments are never changed. */",
			"#define MAX_COUNT 10",
			"#define MSG_HELLO 100",
			"#define LONG_MACRO_NAME(x) (x*2)",
			"",
			"procedure start;",
			"procedure check_item(variable item,variable count);",
			"procedure start",
			"begin",
			"  variable i:=0;",
			"  // A comment stays as it is.",
			"  if (i==0) then begin",
			"     display_msg(\"a+b,c\");",
			"  end else",
			"  begin",
			"      i:=check_item( dude_obj,1 )+LONG_MACRO_NAME(MAX_COUNT-1);",
			"  end",
			"  call check_item(self_obj,i*2-1);",
			"  call show_the_message(MSG_HELLO, \"The first argument\", \"The second argument\", \"The third argument\", i);",
			"end",
			"",
			"",
			"",
			"procedure check_item(variable item,variable count) begin",
			"   return item!=0 and count>=1;   ",
			"end",
		};

		/// <summary>Gets the text of the sample, with CRLF line breaks and a final line break.</summary>
		public static String Text { get; } = String.Join("\r\n", _lines) + "\r\n";
	}
}