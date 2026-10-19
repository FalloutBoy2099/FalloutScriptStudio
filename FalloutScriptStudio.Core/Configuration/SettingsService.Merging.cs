namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Nodes;

	/// <summary>Saving: the decision what to write, the three-way JSON merge and the serialization of the models.</summary>
	public sealed partial class SettingsService
	{
		private static JsonNode? ParseNode(Byte[] content)
		{
			return JsonNode.Parse(content);
		}

		/// <summary>
		/// Three-way merge of the JSON of a file: what this instance changed since it loaded the file (<paramref name="ours"/> against
		/// <paramref name="baseline"/>) is kept, everything else comes from the current content of the file (<paramref name="theirs"/>).
		/// </summary>
		private static JsonNode? MergeNodes(JsonNode? baseline, JsonNode? ours, JsonNode? theirs)
		{
			if (JsonNode.DeepEquals(ours, baseline))
			{
				return theirs?.DeepClone();
			}

			if (JsonNode.DeepEquals(theirs, baseline))
			{
				return ours?.DeepClone();
			}

			if (baseline is JsonObject baseObject && ours is JsonObject ourObject && theirs is JsonObject theirObject)
			{
				JsonObject merged = [];
				foreach (String key in ourObject.Select(pair => pair.Key).Concat(theirObject.Select(pair => pair.Key)).Distinct(StringComparer.Ordinal))
				{
					JsonNode? value = MergeNodes(baseObject[key], ourObject[key], theirObject[key]);
					if (value != null)
					{
						merged[key] = value;
					}
				}

				return merged;
			}

			// Both sides changed the same value (or the shape of it): the instance that saves wins.
			return ours?.DeepClone();
		}

		private SaveAction Decide(String path, Byte[] ours, JsonNode? oursNode)
		{
			if (this._baselines.TryGetValue(path, out JsonNode? baseline))
			{
				return JsonNode.DeepEquals(oursNode, baseline) ? SaveAction.Skip : SaveAction.Merge;
			}

			if (this._loaded)
			{
				// The file did not exist or was damaged when it was loaded: it is (re)created from the current state.
				return SaveAction.WriteOurs;
			}

			// Nothing was loaded (the start-up did not get that far): defaults must not replace the real files.
			return ours.AsSpan().SequenceEqual(this.GetPristine()[path]) ? SaveAction.Skip : SaveAction.WriteOurs;
		}

		/// <summary>
		/// Merges the changes of this instance into the current content of the file.
		/// </summary>
		/// <returns>The bytes to write, or <see langword="null"/> when the file cannot be read now and must be left alone.</returns>
		private Byte[]? Merge(String path, JsonNode? ours, Byte[] oursBytes, FileRead disk)
		{
			if (disk.Failed)
			{
				this._logger.FileNotOverwritten(path);
				return null;
			}

			if (disk.Content == null)
			{
				return oursBytes;
			}

			JsonNode? theirs;
			try
			{
				theirs = ParseNode(disk.Content);
			}
			catch (JsonException)
			{
				return oursBytes;
			}

			JsonNode? merged = MergeNodes(this._baselines[path], ours, theirs);
			return merged == null ? oursBytes : JsonSerializer.SerializeToUtf8Bytes(merged, SettingsJson.Options);
		}

		/// <summary>After a write: what this instance wrote from is the base of the next merge (not the merged result, which holds changes of others).</summary>
		private void Remember(String path, JsonNode? ours)
		{
			if (ours != null)
			{
				this._baselines[path] = ours;
			}
		}

		private Dictionary<String, Byte[]> GetPristine()
		{
			return this._pristine ??= SerializeModels(
				new AppSettings(),
				new RecentFiles(),
				new ScriptPositions(),
				new FoldedProcedures(),
				new SearchHistory(),
				new PreprocessorDefines(),
				this.Paths).ToDictionary(item => item.Path, item => item.Content, StringComparer.OrdinalIgnoreCase);
		}

		private List<(String Path, Byte[] Content)> SerializeAll()
		{
			return SerializeModels(this.Settings, this.Recent, this.ScriptPositions, this.FoldedProcedures, this.SearchHistory, this.PreprocessorDefines, this.Paths);
		}

		private static List<(String Path, Byte[] Content)> SerializeModels(
			AppSettings settings,
			RecentFiles recent,
			ScriptPositions positions,
			FoldedProcedures folding,
			SearchHistory history,
			PreprocessorDefines defines,
			AppPaths paths)
		{
			settings.IsFirstRunCompleted = true;
			settings.Version = AppSettings.CurrentVersion;

			return
			[
				(paths.SettingsFile, JsonSerializer.SerializeToUtf8Bytes(settings, SettingsJson.Options)),
				(paths.RecentFilesFile, JsonSerializer.SerializeToUtf8Bytes(recent, SettingsJson.Options)),
				(paths.ScriptPositionsFile, JsonSerializer.SerializeToUtf8Bytes(positions, SettingsJson.Options)),
				(paths.FoldingFile, JsonSerializer.SerializeToUtf8Bytes(folding, SettingsJson.Options)),
				(paths.SearchHistoryFile, JsonSerializer.SerializeToUtf8Bytes(history, SettingsJson.Options)),
				(paths.PreprocessorDefinesFile, JsonSerializer.SerializeToUtf8Bytes(defines, SettingsJson.Options)),
			];
		}

		private enum SaveAction
		{
			Skip,
			WriteOurs,
			Merge,
		}
	}
}