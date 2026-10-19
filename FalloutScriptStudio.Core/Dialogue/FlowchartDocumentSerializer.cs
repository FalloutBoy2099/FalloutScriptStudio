namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Xml;
	using System.Xml.Linq;

	/// <summary>
	/// Reads and writes the XML of the legacy flowchart files (<c>.fcd</c>), so old files stay compatible.
	/// </summary>
	/// <remarks>
	/// <para>Format (positions and widths are stored divided by 100, booleans as <c>True</c>/<c>False</c>):</para>
	/// <code>
	/// &lt;NodesDiagram ScriptName="x.ssl" Zoom="1" PositionX="0" PositionY="0"&gt;
	///   &lt;Node Name="Node001" Collapsed="False" Hidden="False" ShowCode="True"&gt;
	///     &lt;Position X="0.25" Y="0.25" Width="2.5" /&gt;
	///     &lt;Contents&gt;&lt;Content Line="0" Collapsed="False" /&gt;&lt;/Contents&gt;
	///   &lt;/Node&gt;
	///   &lt;Note NoteText="text"&gt;&lt;Position X="0.4" Y="0.4" Width="2" Height="0.5" /&gt;&lt;/Note&gt;
	/// &lt;/NodesDiagram&gt;
	/// </code>
	/// <para>
	/// Unlike the legacy reader, missing or malformed attributes get default values instead of throwing. The writer produces the
	/// same layout as the legacy one (indented with two spaces, standalone declaration).
	/// </para>
	/// </remarks>
	public static class FlowchartDocumentSerializer
	{
		/// <summary>The file extension of flowchart files.</summary>
		public const String FileExtension = ".fcd";

		private const String RootName = "NodesDiagram";
		private const Single Scale = 100f;

		/// <summary>Reads a flowchart from its XML text.</summary>
		/// <param name="xml">The content of a <c>.fcd</c> file.</param>
		/// <returns>The flowchart.</returns>
		/// <exception cref="FormatException">The text is not XML or does not have a <c>NodesDiagram</c> root.</exception>
		public static FlowchartDocument Parse(String xml)
		{
			ArgumentNullException.ThrowIfNull(xml);

			XDocument document;
			try
			{
				document = XDocument.Parse(xml.TrimStart((Char)0xFEFF));
			}
			catch (XmlException ex)
			{
				throw new FormatException("The flowchart file is not valid XML: " + ex.Message, ex);
			}

			XElement? root = document.Root;
			if (root is null || root.Name.LocalName != RootName)
			{
				throw new FormatException("The file is not a flowchart: the root element must be " + RootName + ".");
			}

			var nodes = new List<FlowchartNodeState>();
			foreach (XElement element in root.Elements("Node"))
			{
				String name = (String?)element.Attribute("Name") ?? String.Empty;
				XElement? position = element.Element("Position");
				var contents = element.Element("Contents")?.Elements("Content")
					.Select(c => new FlowchartContentState((String?)c.Attribute("Line") ?? String.Empty, ReadBoolean(c, "Collapsed")))
					.ToList() ?? [];
				String? showCode = (String?)element.Attribute("ShowCode");

				nodes.Add(new FlowchartNodeState(name)
				{
					X = ReadScaled(position, "X"),
					Y = ReadScaled(position, "Y"),
					Width = ReadScaled(position, "Width"),
					Collapsed = ReadBoolean(element, "Collapsed"),
					Hidden = ReadBoolean(element, "Hidden"),
					ShowCode = String.IsNullOrEmpty(showCode) ? null : Boolean.TryParse(showCode, out Boolean flag) && flag,
					Contents = contents,
				});
			}

			var notes = new List<FlowchartNoteState>();
			foreach (XElement element in root.Elements("Note"))
			{
				XElement? position = element.Element("Position");
				notes.Add(new FlowchartNoteState((String?)element.Attribute("NoteText") ?? String.Empty)
				{
					X = ReadScaled(position, "X"),
					Y = ReadScaled(position, "Y"),
					Width = ReadScaled(position, "Width"),
					Height = ReadScaled(position, "Height"),
				});
			}

			return new FlowchartDocument((String?)root.Attribute("ScriptName") ?? String.Empty)
			{
				Zoom = ReadFloat((String?)root.Attribute("Zoom"), 1f),
				PositionX = ReadInt((String?)root.Attribute("PositionX")),
				PositionY = ReadInt((String?)root.Attribute("PositionY")),
				Nodes = nodes,
				Notes = notes,
			};
		}

		/// <summary>Reads a flowchart without throwing.</summary>
		/// <param name="xml">The content of a <c>.fcd</c> file.</param>
		/// <param name="document">Receives the flowchart.</param>
		/// <param name="error">Receives the reason when the text cannot be read.</param>
		/// <returns><c>true</c> when the flowchart was read.</returns>
		public static Boolean TryParse(String xml, out FlowchartDocument? document, out String? error)
		{
			try
			{
				document = Parse(xml);
				error = null;
				return true;
			}
			catch (FormatException ex)
			{
				document = null;
				error = ex.Message;
				return false;
			}
		}

		/// <summary>Writes a flowchart as XML text.</summary>
		/// <param name="document">The flowchart.</param>
		/// <returns>The XML text (declared as UTF-8).</returns>
		public static String Serialize(FlowchartDocument document)
		{
			return new UTF8Encoding(false).GetString(ToBytes(document));
		}

		/// <summary>Writes a flowchart as the bytes of a <c>.fcd</c> file: UTF-8 with a byte order mark, like the legacy editor.</summary>
		/// <param name="document">The flowchart.</param>
		/// <returns>The file content.</returns>
		public static Byte[] ToFileBytes(FlowchartDocument document)
		{
			Byte[] preamble = new UTF8Encoding(true).GetPreamble();
			Byte[] body = ToBytes(document);
			var result = new Byte[preamble.Length + body.Length];
			preamble.CopyTo(result, 0);
			body.CopyTo(result, preamble.Length);
			return result;
		}

		private static Byte[] ToBytes(FlowchartDocument document)
		{
			ArgumentNullException.ThrowIfNull(document);

			using var stream = new MemoryStream();
			var settings = new XmlWriterSettings
			{
				Indent = true,
				IndentChars = "  ",
				NewLineChars = "\r\n",
				Encoding = new UTF8Encoding(false),
			};

			using (XmlWriter writer = XmlWriter.Create(stream, settings))
			{
				writer.WriteStartDocument(true);
				writer.WriteStartElement(RootName);
				writer.WriteAttributeString("ScriptName", document.ScriptName);
				writer.WriteAttributeString("Zoom", FormatFloat(document.Zoom));
				writer.WriteAttributeString("PositionX", document.PositionX.ToString(CultureInfo.InvariantCulture));
				writer.WriteAttributeString("PositionY", document.PositionY.ToString(CultureInfo.InvariantCulture));

				foreach (FlowchartNodeState node in document.Nodes)
				{
					writer.WriteStartElement("Node");
					writer.WriteAttributeString("Name", node.Name);
					writer.WriteAttributeString("Collapsed", node.Collapsed.ToString());
					writer.WriteAttributeString("Hidden", node.Hidden.ToString());
					if (node.ShowCode is not null)
					{
						writer.WriteAttributeString("ShowCode", node.ShowCode.Value.ToString());
					}

					WritePosition(writer, node.X, node.Y, node.Width, null);
					if (node.Contents.Count > 0)
					{
						writer.WriteStartElement("Contents");
						foreach (FlowchartContentState content in node.Contents)
						{
							writer.WriteStartElement("Content");
							writer.WriteAttributeString("Line", content.Line);
							writer.WriteAttributeString("Collapsed", content.Collapsed.ToString());
							writer.WriteEndElement();
						}

						writer.WriteEndElement();
					}

					writer.WriteEndElement();
				}

				foreach (FlowchartNoteState note in document.Notes)
				{
					writer.WriteStartElement("Note");
					writer.WriteAttributeString("NoteText", note.Text);
					WritePosition(writer, note.X, note.Y, note.Width, note.Height);
					writer.WriteEndElement();
				}

				writer.WriteEndElement();
				writer.WriteEndDocument();
			}

			return stream.ToArray();
		}

		private static void WritePosition(XmlWriter writer, Single x, Single y, Single width, Single? height)
		{
			writer.WriteStartElement("Position");
			writer.WriteAttributeString("X", FormatFloat(x / Scale));
			writer.WriteAttributeString("Y", FormatFloat(y / Scale));
			writer.WriteAttributeString("Width", FormatFloat(width / Scale));
			if (height is not null)
			{
				writer.WriteAttributeString("Height", FormatFloat(height.Value / Scale));
			}

			writer.WriteEndElement();
		}

		private static String FormatFloat(Single value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		private static Single ReadScaled(XElement? element, String attribute)
		{
			return Scale * ReadFloat((String?)element?.Attribute(attribute), 0f);
		}

		private static Single ReadFloat(String? text, Single fallback)
		{
			return Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out Single value) ? value : fallback;
		}

		private static Int32 ReadInt(String? text)
		{
			return Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 value) ? value : 0;
		}

		private static Boolean ReadBoolean(XElement element, String attribute)
		{
			return Boolean.TryParse((String?)element.Attribute(attribute), out Boolean value) && value;
		}
	}
}