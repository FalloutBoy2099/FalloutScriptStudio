namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Finds the message files a dialogue needs and could not be found (legacy <c>MessageFile.ShowMissingFiles</c>, shown after a
	/// flowchart is built).
	/// </summary>
	public static class MissingMessageFiles
	{
		/// <summary>
		/// Lists the files of the dialogue messages that were not found: files other than the message file of the script itself,
		/// designated by a file number of <c>scripts.lst</c>.
		/// </summary>
		/// <param name="model">The flowchart.</param>
		/// <param name="source">The message source the flowchart was built with; its files must be loaded already.</param>
		/// <returns>The expected paths of the missing files, each once, in the order they are first needed.</returns>
		public static IReadOnlyList<String> Find(FlowchartModel model, IDialogueMessageSource source)
		{
			ArgumentNullException.ThrowIfNull(model);
			ArgumentNullException.ThrowIfNull(source);

			List<String> paths = [];
			foreach (FlowchartContentLine line in model.Nodes.SelectMany(node => node.Content))
			{
				if (line.MessageFileNumber <= 0 || line.MessageNumber <= 0 || !line.Opcode.HasMessage())
				{
					continue;
				}

				DialogueMessageLookup lookup = source.GetMessage(line.MessageFileNumber, line.MessageNumber);
				if (lookup.Status == DialogueMessageStatus.FileNotFound && lookup.FilePath is { } path && !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
				{
					paths.Add(path);
				}
			}

			return paths;
		}
	}
}