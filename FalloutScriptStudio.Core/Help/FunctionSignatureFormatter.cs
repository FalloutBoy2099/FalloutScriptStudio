namespace FalloutScriptStudio.Core.Help
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Analysis;

	/// <summary>Turns the signature of a function into pieces that the help colors: the result type, the name, and each parameter with its type and name.</summary>
	public static class FunctionSignatureFormatter
	{
		/// <summary>Formats a signature the way the script writes the call: a function without parameters has no parentheses.</summary>
		/// <param name="signature">The signature.</param>
		/// <returns>The pieces, in order.</returns>
		public static IReadOnlyList<SignatureSegment> Format(FunctionSignature signature)
		{
			ArgumentNullException.ThrowIfNull(signature);

			List<SignatureSegment> segments = [];
			if (signature.ReturnType.Length > 0)
			{
				segments.Add(new SignatureSegment(signature.ReturnType, SignatureSegmentKind.ReturnType));
				segments.Add(new SignatureSegment(" ", SignatureSegmentKind.Punctuation));
			}

			segments.Add(new SignatureSegment(signature.Name, SignatureSegmentKind.Name));
			if (!signature.TakesParentheses)
			{
				return segments;
			}

			segments.Add(new SignatureSegment("(", SignatureSegmentKind.Punctuation));
			Boolean first = true;
			foreach (FunctionParameter parameter in signature.Parameters)
			{
				if (!first)
				{
					segments.Add(new SignatureSegment(", ", SignatureSegmentKind.Punctuation));
				}

				first = false;
				if (parameter.IsVariadic)
				{
					segments.Add(new SignatureSegment("...", SignatureSegmentKind.Variadic));
					continue;
				}

				if (parameter.IsOptional)
				{
					segments.Add(new SignatureSegment("[", SignatureSegmentKind.Punctuation));
				}

				if (parameter.Type.Length > 0)
				{
					segments.Add(new SignatureSegment(parameter.Type, SignatureSegmentKind.ParameterType));
					if (parameter.Name.Length > 0)
					{
						segments.Add(new SignatureSegment(" ", SignatureSegmentKind.Punctuation));
					}
				}

				if (parameter.Name.Length > 0)
				{
					segments.Add(new SignatureSegment(parameter.Name, SignatureSegmentKind.ParameterName));
				}

				if (parameter.IsOptional)
				{
					segments.Add(new SignatureSegment("]", SignatureSegmentKind.Punctuation));
				}
			}

			segments.Add(new SignatureSegment(")", SignatureSegmentKind.Punctuation));
			return segments;
		}

		/// <summary>Joins the pieces of a signature into one line.</summary>
		/// <param name="segments">The pieces.</param>
		/// <returns>The text.</returns>
		public static String ToText(IEnumerable<SignatureSegment> segments)
		{
			ArgumentNullException.ThrowIfNull(segments);

			StringBuilder text = new();
			foreach (SignatureSegment segment in segments)
			{
				text.Append(segment.Text);
			}

			return text.ToString();
		}
	}
}