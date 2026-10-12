namespace FalloutScriptStudio.Tests.Diagram
{
	using System;
	using FalloutScriptStudio.Controls.Diagram.Model;

	internal static class DiagramCanvasTestHelpers
	{
		internal static DiagramNode CreateNode(String name, Double x = 0, Double y = 0)
		{
			DiagramNode node = new DiagramNode(name) { X = x, Y = y };
			node.HeaderLines.Add("talk_p_proc");
			node.AddRow(DiagramRowKind.Code, "start_gdialog(NPC_MSG, self_obj, 4, -1, -1);");
			node.AddRow(DiagramRowKind.Reply, "\"Hello there, stranger.\"", "Reply(100);");
			return node;
		}
	}
}
