namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using TextTools;

	/// <summary>
	/// Extracts the highlighted keyword from the code of a user function (legacy <c>Functions.GetKeyWord</c>).
	/// </summary>
	public static class FunctionKeyword
	{
		/// <summary>
		/// Gets the leading identifier of a function's code: <c>DEBUGMSG({string})</c> gives <c>DEBUGMSG</c>.
		/// </summary>
		/// <param name="code">The code of the function.</param>
		/// <returns>The identifier, which is empty when the code does not start with one.</returns>
		public static String Extract(String code)
		{
			ArgumentNullException.ThrowIfNull(code);

			String trimmed = code.TrimStart();
			Int32 end = 0;
			while (end < trimmed.Length && WordScanner.IsWordChar(trimmed[end]))
			{
				end++;
			}

			return trimmed.Substring(0, end);
		}
	}
}