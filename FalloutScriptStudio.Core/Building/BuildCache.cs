namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.Json;
	using Microsoft.Extensions.Logging;
	using Configuration;

	/// <summary>
	/// The build cache (<c>fss-build.json</c>): for every script and output file the last successful compilation, with the stamps of the
	/// source and of each file it included. Nothing in it is trusted blindly: a file that is damaged, from another version or locked
	/// gives an empty cache, which makes every script stale. The file is written atomically. Thread safe.
	/// </summary>
	public sealed class BuildCache
	{
		/// <summary>The name of the cache file.</summary>
		public const String FileName = "fss-build.json";

		/// <summary>The version of the file layout.</summary>
		public const Int32 FormatVersion = 1;

		private readonly Dictionary<String, BuildRecord> _records = new(StringComparer.OrdinalIgnoreCase);
		private readonly Object _sync = new Object();
		private readonly Object _saveSync = new Object();
		private readonly ILogger? _logger;
		private Boolean _dirty;
		private Boolean _saving;

		private BuildCache(String filePath, ILogger? logger)
		{
			this.FilePath = filePath;
			this._logger = logger;
		}

		/// <summary>Gets the path of the cache file.</summary>
		public String FilePath { get; }

		/// <summary>Gets how the file was found when it was loaded.</summary>
		public BuildCacheStatus Status { get; private set; } = BuildCacheStatus.Missing;

		/// <summary>Gets the stamp the cache file had when it was loaded or last written by this instance.</summary>
		public FileStamp LoadedStamp { get; private set; } = FileStamp.Missing;

		/// <summary>Gets the number of records.</summary>
		public Int32 Count
		{
			get
			{
				lock (this._sync)
				{
					return this._records.Count;
				}
			}
		}

		/// <summary>Gets a value indicating whether there are changes that were not written yet.</summary>
		public Boolean IsDirty
		{
			get
			{
				lock (this._sync)
				{
					return this._dirty || this._saving;
				}
			}
		}

		/// <summary>Gets the location of the cache file for the active project, or for the settings folder when there is no project.</summary>
		/// <param name="paths">The paths of the installation.</param>
		/// <param name="projectFolder">The folder of the active project, or <see langword="null"/>.</param>
		/// <returns>The full path of the cache file.</returns>
		public static String GetFilePath(AppPaths paths, String? projectFolder)
		{
			ArgumentNullException.ThrowIfNull(paths);
			return String.IsNullOrWhiteSpace(projectFolder) ? paths.BuildCacheFile : Path.Combine(projectFolder, FileName);
		}

		/// <summary>Reads a cache file. This never throws: whatever is wrong with the file, the result is an empty cache.</summary>
		/// <param name="filePath">The cache file.</param>
		/// <param name="logger">Receives the reason when the file is damaged or unreadable, or <see langword="null"/>.</param>
		/// <returns>The cache.</returns>
		public static BuildCache Load(String filePath, ILogger? logger = null)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);

			BuildCache cache = new BuildCache(Path.GetFullPath(filePath), logger);
			Byte[] content;
			try
			{
				cache.LoadedStamp = FileStamp.Read(cache.FilePath);
				if (!cache.LoadedStamp.Exists)
				{
					return cache;
				}

				content = File.ReadAllBytes(cache.FilePath);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				cache.Status = BuildCacheStatus.Unreadable;
				logger?.CacheUnreadable(ex, cache.FilePath);
				return cache;
			}

			try
			{
				cache.Parse(content);
				cache.Status = BuildCacheStatus.Loaded;
			}
			catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException or KeyNotFoundException or OverflowException or IndexOutOfRangeException)
			{
				cache._records.Clear();
				cache.Status = BuildCacheStatus.Corrupt;
				logger?.CacheDamaged(ex, cache.FilePath);
			}

			return cache;
		}

		/// <summary>Finds the record of a script and output file.</summary>
		/// <param name="script">The full path of the script.</param>
		/// <param name="output">The full path of the compiled file.</param>
		/// <param name="record">The record when found.</param>
		/// <returns><see langword="true"/> when there is a record.</returns>
		public Boolean TryGet(String script, String output, out BuildRecord? record)
		{
			lock (this._sync)
			{
				return this._records.TryGetValue(Key(script, output), out record);
			}
		}

		/// <summary>Stores the record of a successful compilation, replacing the previous one.</summary>
		/// <param name="record">The record.</param>
		public void Set(BuildRecord record)
		{
			ArgumentNullException.ThrowIfNull(record);
			lock (this._sync)
			{
				this._records[Key(record.Script, record.Output)] = record;
				this._dirty = true;
			}
		}

		/// <summary>Removes the record of a script and output file.</summary>
		/// <param name="script">The full path of the script.</param>
		/// <param name="output">The full path of the compiled file.</param>
		/// <returns><see langword="true"/> when there was a record.</returns>
		public Boolean Remove(String script, String output)
		{
			lock (this._sync)
			{
				if (!this._records.Remove(Key(script, output)))
				{
					return false;
				}

				this._dirty = true;
				return true;
			}
		}

		/// <summary>Removes the records of scripts that do not exist any more.</summary>
		/// <param name="exists">Tells whether a script still exists.</param>
		/// <returns>The number of records removed.</returns>
		public Int32 RemoveMissingScripts(Func<String, Boolean> exists)
		{
			ArgumentNullException.ThrowIfNull(exists);
			List<BuildRecord> snapshot;
			lock (this._sync)
			{
				snapshot = this._records.Values.ToList();
			}

			Int32 removed = 0;
			foreach (BuildRecord record in snapshot)
			{
				if (!exists(record.Script) && this.Remove(record.Script, record.Output))
				{
					removed++;
				}
			}

			return removed;
		}

		/// <summary>Writes the cache file when something changed. A file that cannot be written is logged and tried again at the next save.</summary>
		/// <returns><see langword="true"/> when the file is up to date afterwards.</returns>
		public Boolean Save()
		{
			lock (this._saveSync)
			{
				Byte[] content;
				lock (this._sync)
				{
					if (!this._dirty)
					{
						return true;
					}

					content = this.Serialize();
					this._dirty = false;
					this._saving = true;
				}

				try
				{
					AtomicFile.WriteAllBytes(this.FilePath, content);
					this.LoadedStamp = FileStamp.Read(this.FilePath);
					return true;
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					lock (this._sync)
					{
						this._dirty = true;
					}

					this._logger?.CacheNotSaved(ex, this.FilePath);
					return false;
				}
				finally
				{
					lock (this._sync)
					{
						this._saving = false;
					}
				}
			}
		}

		private static String Key(String script, String output)
		{
			return script + "\n" + output;
		}

		private static void WriteStamp(Utf8JsonWriter writer, String name, FileStamp stamp)
		{
			writer.WriteStartArray(name);
			writer.WriteNumberValue(stamp.Length);
			writer.WriteNumberValue(stamp.UtcTicks);
			writer.WriteEndArray();
		}

		private static FileStamp ReadStamp(JsonElement element)
		{
			if (element.GetArrayLength() != 2)
			{
				throw new JsonException("A stamp needs a size and a time.");
			}

			return new FileStamp(element[0].GetInt64(), element[1].GetInt64());
		}

		private Byte[] Serialize()
		{
			Dictionary<String, Int32> indexes = new(StringComparer.OrdinalIgnoreCase);
			List<String> paths = [];
			Int32 IndexOf(String path)
			{
				if (!indexes.TryGetValue(path, out Int32 index))
				{
					index = paths.Count;
					indexes[path] = index;
					paths.Add(path);
				}

				return index;
			}

			// The entries are written first because they define the path table, which comes before them in the file.
			ArrayBufferWriter<Byte> entriesBuffer = new ArrayBufferWriter<Byte>(1 << 16);
			using (Utf8JsonWriter entries = new Utf8JsonWriter(entriesBuffer))
			{
				entries.WriteStartArray();
				foreach (BuildRecord record in this._records.Values.OrderBy(item => item.Script, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Output, StringComparer.OrdinalIgnoreCase))
				{
					entries.WriteStartObject();
					entries.WriteNumber("script", IndexOf(record.Script));
					entries.WriteNumber("output", IndexOf(record.Output));
					entries.WriteString("options", record.Fingerprint.Options);
					entries.WriteString("tools", record.Fingerprint.Tools);
					entries.WriteBoolean("shortCircuit", record.ShortCircuit);
					WriteStamp(entries, "source", record.Source);
					WriteStamp(entries, "outputStamp", record.OutputStamp);
					entries.WriteStartArray("dependencies");
					foreach (DependencyStamp dependency in record.Dependencies)
					{
						entries.WriteNumberValue(IndexOf(dependency.Path));
						entries.WriteNumberValue(dependency.Stamp.Length);
						entries.WriteNumberValue(dependency.Stamp.UtcTicks);
					}

					entries.WriteEndArray();
					entries.WriteEndObject();
				}

				entries.WriteEndArray();
			}

			ArrayBufferWriter<Byte> buffer = new ArrayBufferWriter<Byte>(entriesBuffer.WrittenCount + (1 << 12));
			using (Utf8JsonWriter writer = new Utf8JsonWriter(buffer))
			{
				writer.WriteStartObject();
				writer.WriteNumber("version", FormatVersion);
				writer.WriteStartArray("paths");
				foreach (String path in paths)
				{
					writer.WriteStringValue(path);
				}

				writer.WriteEndArray();
				writer.WritePropertyName("entries");
				writer.WriteRawValue(entriesBuffer.WrittenSpan, true);
				writer.WriteEndObject();
			}

			return buffer.WrittenSpan.ToArray();
		}

		private void Parse(Byte[] content)
		{
			using JsonDocument document = JsonDocument.Parse(content);
			JsonElement root = document.RootElement;
			if (root.ValueKind != JsonValueKind.Object || root.GetProperty("version").GetInt32() != FormatVersion)
			{
				throw new JsonException("Not a build cache of this version.");
			}

			String[] paths = root.GetProperty("paths").EnumerateArray().Select(item => item.GetString() ?? throw new JsonException("A path is empty.")).ToArray();
			foreach (JsonElement entry in root.GetProperty("entries").EnumerateArray())
			{
				JsonElement dependencies = entry.GetProperty("dependencies");
				Int32 count = dependencies.GetArrayLength();
				if (count % 3 != 0)
				{
					throw new JsonException("The dependencies are not triples.");
				}

				List<DependencyStamp> list = new(count / 3);
				for (Int32 index = 0; index < count; index += 3)
				{
					list.Add(new DependencyStamp(paths[dependencies[index].GetInt32()], new FileStamp(dependencies[index + 1].GetInt64(), dependencies[index + 2].GetInt64())));
				}

				BuildRecord record = new BuildRecord(
					paths[entry.GetProperty("script").GetInt32()],
					paths[entry.GetProperty("output").GetInt32()],
					new BuildFingerprint(entry.GetProperty("options").GetString() ?? String.Empty, entry.GetProperty("tools").GetString() ?? String.Empty),
					entry.GetProperty("shortCircuit").GetBoolean(),
					ReadStamp(entry.GetProperty("source")),
					ReadStamp(entry.GetProperty("outputStamp")),
					list);
				this._records[Key(record.Script, record.Output)] = record;
			}
		}
	}
}