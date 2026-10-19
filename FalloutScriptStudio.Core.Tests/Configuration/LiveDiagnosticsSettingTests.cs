namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Text.Json;
	using System.Text.Json.Serialization;
	using FalloutScriptStudio.Core.Configuration;
	using Xunit;

	public sealed class LiveDiagnosticsSettingTests
	{
		private static JsonSerializerOptions Options()
		{
			JsonSerializerOptions options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
			options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
			return options;
		}

		[Fact]
		public void The_default_shows_errors_and_warnings()
		{
			Assert.Equal(LiveDiagnosticsLevel.ErrorsAndWarnings, new EditorSettings().LiveDiagnostics);
		}

		[Theory]
		[InlineData(LiveDiagnosticsLevel.Off, "off")]
		[InlineData(LiveDiagnosticsLevel.ErrorsOnly, "errorsOnly")]
		[InlineData(LiveDiagnosticsLevel.ErrorsAndWarnings, "errorsAndWarnings")]
		public void The_names_stored_in_the_settings_file_are_stable(LiveDiagnosticsLevel level, String name)
		{
			String json = JsonSerializer.Serialize(new EditorSettings { LiveDiagnostics = level }, Options());

			Assert.Contains("\"liveDiagnostics\":\"" + name + "\"", json, StringComparison.Ordinal);
			Assert.Equal(level, JsonSerializer.Deserialize<EditorSettings>("{\"liveDiagnostics\":\"" + name + "\"}", Options())!.LiveDiagnostics);
		}

		[Fact]
		public void A_file_written_by_an_older_version_gets_the_default()
		{
			EditorSettings editor = JsonSerializer.Deserialize<EditorSettings>("{\"tabSize\":4}", Options())!;

			Assert.Equal(LiveDiagnosticsLevel.ErrorsAndWarnings, editor.LiveDiagnostics);
		}

		[Fact]
		public void A_value_that_is_not_a_level_is_repaired_when_the_settings_are_loaded()
		{
			AppSettings settings = new AppSettings();
			settings.Editor.LiveDiagnostics = (LiveDiagnosticsLevel)9;

			settings.Normalize();

			Assert.Equal(LiveDiagnosticsLevel.ErrorsAndWarnings, settings.Editor.LiveDiagnostics);
		}

		[Fact]
		public void The_level_survives_a_clone_of_the_settings()
		{
			AppSettings settings = new AppSettings();
			settings.Editor.LiveDiagnostics = LiveDiagnosticsLevel.Off;

			Assert.Equal(LiveDiagnosticsLevel.Off, settings.Clone().Editor.LiveDiagnostics);
		}
	}
}