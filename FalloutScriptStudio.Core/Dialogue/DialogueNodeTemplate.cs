namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// A starting body for a new dialogue node (legacy <c>TemplateNode</c> list).
	/// </summary>
	/// <param name="Name">The name shown in the template list.</param>
	/// <param name="Code">The body of the node procedure.</param>
	public sealed record DialogueNodeTemplate(String Name, String Code)
	{
		/// <summary>Gets the node name suggested in the "new node" dialog; the <c>###</c> is to be replaced by the user.</summary>
		public const String DefaultNodeName = "Node###";

		/// <summary>Gets the templates the legacy editor offered, the empty one first.</summary>
		public static System.Collections.Generic.IReadOnlyList<DialogueNodeTemplate> All { get; } = new[]
		{
			new DialogueNodeTemplate("<Empty>", String.Empty),
			new DialogueNodeTemplate("Template Code 1", "\r\n   Reply(100);\r\n   NOption(101, Node999, 4);\r\n   NLowOption(102, Node999);\r\n"),
			new DialogueNodeTemplate("Template Code 2", "\r\n   Reply(100);\r\n   if (true) then\r\n      NOption(101, Node999, 4);\r\n   else\r\n      NOption(102, Node999, 4);\r\n"),
			new DialogueNodeTemplate("Template Code 3", "\r\n   NMessage(100);\r\n"),
		};
	}
}