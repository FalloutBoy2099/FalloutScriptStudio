namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>Reads the body of a snippet from a text or from a list of lines, and writes it as a list of lines (easier to read and to compare in a file).</summary>
	internal sealed class SnippetBodyConverter : JsonConverter<String>
	{
		/// <inheritdoc />
		public override String Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				return reader.GetString() ?? String.Empty;
			}

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				throw new JsonException("The body of a snippet is a text or a list of lines.");
			}

			List<String> lines = [];
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				if (reader.TokenType != JsonTokenType.String)
				{
					throw new JsonException("The lines of the body of a snippet are texts.");
				}

				lines.Add(reader.GetString() ?? String.Empty);
			}

			return String.Join("\n", lines);
		}

		/// <inheritdoc />
		public override void Write(Utf8JsonWriter writer, String value, JsonSerializerOptions options)
		{
			if (value.IndexOf('\n', StringComparison.Ordinal) < 0 && value.IndexOf('\r', StringComparison.Ordinal) < 0)
			{
				writer.WriteStringValue(value);
				return;
			}

			writer.WriteStartArray();
			foreach (String line in value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
			{
				writer.WriteStringValue(line);
			}

			writer.WriteEndArray();
		}
	}
}