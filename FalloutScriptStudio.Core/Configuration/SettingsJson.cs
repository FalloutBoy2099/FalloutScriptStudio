namespace FalloutScriptStudio.Core.Configuration
{
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>
	/// The JSON options shared by all persisted models: indented, camelCase, enums as text.
	/// </summary>
	internal static class SettingsJson
	{
		public static readonly JsonSerializerOptions Options = CreateOptions();

		private static JsonSerializerOptions CreateOptions()
		{
			JsonSerializerOptions options = new JsonSerializerOptions
			{
				WriteIndented = true,
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			};
			options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
			return options;
		}
	}
}