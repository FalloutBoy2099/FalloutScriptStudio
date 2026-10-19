namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Parsing;
	using TextTools;

	/// <summary>Parsing of each kind of dialogue statement: replies, messages, options, macros that jump to a node and <c>call</c>.</summary>
	internal sealed partial class DialogueLineBuilder
	{
		/// <summary>gsay_message(msg_list, msg_num, reaction); gsay_reply(msg_list, msg_num); Reply / Message macros.</summary>
		private DialogueOpcode ReplyOpcode(String name, String code, Int32 offset)
		{
			Int32 x = GetOpcodeEndPosition(code, offset);
			this.SetNextPosition(ref x, offset);

			String lineMsg;
			if (this._rules.TryGet(name, out DialogueFunctionRule? template))
			{
				if (template.TotalArgs > 1)
				{
					if (CountArguments(code) != template.TotalArgs)
					{
						lineMsg = this.ReplyParseCode(offset, x, name, ref code);
					}
					else
					{
						code = code.Replace(';', ' ');
						String[] args = code.Substring(offset + 1).Split(',', template.TotalArgs + 1);
						lineMsg = args[template.MessageArg];
						if (template.MessageFileArg != -1)
						{
							this._numberMsgFile = this.ParseMessageFile(args[template.MessageFileArg]);
						}
					}
				}
				else
				{
					Int32 z = code.IndexOf(')', offset);
					lineMsg = code.Substring(offset + 1, z - offset);
				}

				if (this._shortCode is null)
				{
					Int32 start = offset - name.Length;
					this._shortCode = this._fullCode.Substring(start, (x > -1 ? x : this._fullCode.Length - 1) - start + 1);
				}
			}
			else
			{
				lineMsg = this.ReplyParseCode(offset, x, name, ref code);
			}

			if (lineMsg.Length > 0)
			{
				this._numberMsgLine = TryParseInt(lineMsg, out Int32 number) ? number : this.MessageSubCode(lineMsg);
			}

			if (this._opcode is DialogueOpcode.Message or DialogueOpcode.GsayMessage)
			{
				this._toNode = "[Message]";
				return DialogueOpcode.Message;
			}

			this._toNode = "[Reply]";
			return DialogueOpcode.Reply;
		}

		private String ReplyParseCode(Int32 offset, Int32 x, String name, ref String code)
		{
			if (x < 0)
			{
				return String.Empty;
			}

			Int32 start = offset - name.Length;
			this._shortCode = code.Substring(start, x + 1 - start);
			if (code[x] == ')')
			{
				x--;
			}

			try
			{
				code = code.Substring(offset + 1, x - offset);
			}
			catch (ArgumentOutOfRangeException)
			{
				return String.Empty;
			}

			if (this._opcode == DialogueOpcode.GsayMessage)
			{
				code = code.Remove(code.LastIndexOf(','));
			}

			if (this._opcode is DialogueOpcode.GsayReply or DialogueOpcode.GsayMessage)
			{
				Int32 z = code.IndexOf(',');
				String fileNumber = code.Remove(z++);
				this._numberMsgFile = this.ParseMessageFile(fileNumber);
				code = code.Substring(z);
			}

			return code;
		}

		/// <summary>gsay_option(msg_list, msg_num, procedure, reaction); giq_option(iq, msg_list, msg_num, procedure, reaction); NOption(msg, node, iq) macros.</summary>
		private void OptionOpcode(Int32 offset, String name, String code)
		{
			Int32 x = GetOpcodeEndPosition(code, offset);
			this.SetNextPosition(ref x, offset);

			String lineMsg;
			if (this._rules.TryGet(name, out DialogueFunctionRule? template))
			{
				if (CountArguments(code) != template.TotalArgs)
				{
					lineMsg = this.ParseOptionCode(offset, x, name, ref code);
				}
				else
				{
					code = code.Replace(';', ' ');
					String[] args = code.Substring(offset + 1).Split(',', template.TotalArgs + 1);

					lineMsg = args[template.MessageArg];
					this._toNode = CleanNodeName(args[template.NodeArg]);

					if (template.MessageFileArg != -1)
					{
						this._numberMsgFile = this.ParseMessageFile(args[template.MessageFileArg]);
					}

					if (template.IqArg != -1)
					{
						if (TryParseInt(args[template.IqArg], out Int32 iq))
						{
							this._iq = iq.ToString(CultureInfo.InvariantCulture);
						}
					}
					else
					{
						this._iq = "-";
					}

					Int32 start = offset - name.Length;
					this._shortCode = code.Substring(start, (x > -1 ? x : code.Length - 1) - start + 1);
					code = args[template.MessageArg];
				}
			}
			else
			{
				lineMsg = this.ParseOptionCode(offset, x, name, ref code);
			}

			if (lineMsg.Length > 0 && !TryParseInt(lineMsg, out this._numberMsgLine))
			{
				this._numberMsgLine = this.MessageSubCode(code);
			}
		}

		private String ParseOptionCode(Int32 offset, Int32 x, String name, ref String code)
		{
			if (x < offset)
			{
				this._opcode = DialogueOpcode.None;
				return String.Empty;
			}

			Int32 start = offset - name.Length;
			this._shortCode = code.Substring(start, x + 1 - start);
			try
			{
				code = code.Substring(++offset, x - offset);
			}
			catch (ArgumentOutOfRangeException)
			{
				return String.Empty;
			}

			Int32 z = code.LastIndexOf(',') + 1;
			String msgNum = String.Empty;

			switch (this._opcode)
			{
				case DialogueOpcode.GsayOption:
				case DialogueOpcode.GiqOption:
					if (this._opcode == DialogueOpcode.GiqOption)
					{
						x = code.IndexOf(',');
						this._iq = code.Remove(x).Trim(_trimming);
						code = code.Substring(++x);
						z -= x;
					}
					else
					{
						this._iq = "-";
					}

					code = code.Remove(z - 1);
					z = code.LastIndexOf(',') + 1;
					this._toNode = CleanNodeName(code.Substring(z));
					code = code.Remove(z - 1);

					z = code.IndexOf(',');
					String fileNumber = code.Remove(z);
					x = fileNumber.IndexOf(',') + 1;
					this._numberMsgFile = TryParseInt(fileNumber.Substring(x), out Int32 file) ? file : this.MessageSubCode(fileNumber);
					code = code.Substring(z + 1);

					msgNum = code;
					z = msgNum.IndexOf(',');
					if (z > 0)
					{
						msgNum = msgNum.Remove(z);
					}

					break;

				case DialogueOpcode.Option:
					String last = code.Substring(z).Trim(_trimming);
					if (Int32.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 iq))
					{
						this._iq = iq.ToString(CultureInfo.InvariantCulture);
						code = code.Remove(z - 1);
					}
					else
					{
						this._toNode = last.TrimStart('@');
					}

					if (this._toNode is null)
					{
						Int32 y = code.LastIndexOf(',') + 1;
						this._toNode = CleanNodeName(code.Substring(y, --z - y));
						z = y;
					}

					msgNum = code = code.Remove(z - 1);
					break;
			}

			return msgNum;
		}

		/// <summary>User macros that jump to a node without a message (rules of type <c>Call</c>).</summary>
		private DialogueOpcode MiscCode(String name, String code, Int32 offset)
		{
			Int32 x = GetOpcodeEndPosition(code, offset);
			this.SetNextPosition(ref x, offset);

			DialogueFunctionRule template = this._rules.TryGet(name, out DialogueFunctionRule? found) ? found : throw new KeyNotFoundException(name);
			if (template.TotalArgs > 1)
			{
				if (CountArguments(code) != template.TotalArgs)
				{
					this._toNode = this.FindNodeByKeyword(code, offset) ?? "<Failed get node name>";
				}
				else
				{
					code = code.Replace(';', ' ');
					String[] args = code.Substring(offset + 1).Split(',', template.TotalArgs + 1);
					this._toNode = CleanNodeName(args[template.NodeArg]);
				}
			}
			else
			{
				Int32 z = code.IndexOf(')', offset);
				this._toNode = CleanNodeName(code.Substring(offset + 1, z - offset));
			}

			Int32 start = offset - name.Length;
			this._shortCode = this._fullCode.Substring(start, (x > -1 ? x : this._fullCode.Length - 1) - start + 1);
			this._numberMsgLine = -1;
			return DialogueOpcode.Call;
		}

		/// <summary>Finds a procedure name that contains the word "node" in a statement whose arguments cannot be split reliably.</summary>
		private String? FindNodeByKeyword(String code, Int32 offset)
		{
			Int32 z = offset;
			while (z != -1 && z < code.Length)
			{
				z = code.IndexOf("node", z, StringComparison.OrdinalIgnoreCase);
				if (z == -1)
				{
					break;
				}

				String word = WordScanner.GetWordAt(code, z);
				ScriptProcedure? procedure = word.Length == 0 ? null : this._info.GetProcedure(word);
				if (procedure is not null)
				{
					return procedure.Name;
				}

				z++;
			}

			return null;
		}

		/// <summary>The standard <c>call Node;</c> statement.</summary>
		private void CallOpcode(String code, Int32 offset)
		{
			Int32 end = code.IndexOf(';', offset);
			if (end == -1)
			{
				end = code.Length;
			}

			Int32 start = offset - 4;
			this._shortCode = code.Substring(start, end - start);

			Int32 paren = code.IndexOf('(', offset);
			if (paren > 0 && paren < end)
			{
				end = paren;
			}

			String target = code.Substring(offset, end - offset).Trim();
			Int32 length = 0;
			while (length < target.Length && WordScanner.IsWordChar(target[length]))
			{
				length++;
			}

			this._toNode = length > 0 ? target.Substring(0, length) : "<Failed get call node name>";
			this._numberMsgLine = -1;
		}
	}
}