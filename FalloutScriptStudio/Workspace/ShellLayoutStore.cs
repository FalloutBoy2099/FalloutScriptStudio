namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;
	using System.Text.Json;
	using System.Text.Json.Serialization;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>Loads and saves the <see cref="ShellLayout"/> next to the other settings (<c>settings\shell.json</c>).</summary>
	public sealed class ShellLayoutStore
	{
		private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
		{
			WriteIndented = true,
			Converters = { new JsonStringEnumConverter() },
		};

		private readonly String _path;

		/// <summary>Initializes the store.</summary>
		public ShellLayoutStore(AppPaths paths)
		{
			ArgumentNullException.ThrowIfNull(paths);
			this._path = Path.Combine(paths.SettingsDirectory, "shell.json");
		}

		/// <summary>Gets a value indicating whether a layout file exists.</summary>
		public Boolean Exists => File.Exists(this._path);

		/// <summary>Reads the layout; the defaults when the file is missing or damaged.</summary>
		public ShellLayout Load()
		{
			try
			{
				if (File.Exists(this._path))
				{
					return JsonSerializer.Deserialize<ShellLayout>(File.ReadAllText(this._path), _options) ?? new ShellLayout();
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
			{
				// A damaged layout file is not worth failing the start for.
			}

			return new ShellLayout();
		}

		/// <summary>Writes the layout; errors are ignored.</summary>
		public void Save(ShellLayout layout)
		{
			ArgumentNullException.ThrowIfNull(layout);
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(this._path)!);
				File.WriteAllText(this._path, JsonSerializer.Serialize(layout, _options));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// Losing the view state is harmless.
			}
		}
	}
}
