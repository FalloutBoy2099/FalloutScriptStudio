namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>The pass over the preprocessor lines.</summary>
	internal sealed partial class ScriptAnalyzer
	{
		/// <summary>
		/// Reads the <c>#define</c> and <c>#include</c> lines and works out, for every token, the conditional block it is in. Only one branch of
		/// an <c>#if</c> group is analysed (the first one, the second one after <c>#if 0</c>): the other branches are compiled only under
		/// another condition, so they are not checked, but the names they declare are remembered so that they are never reported missing.
		/// </summary>
		private void ScanDirectives()
		{
			this._frames.Add(new ConditionFrame(0, null, 0, 0, 0));
			this._frameOf = new Int32[this._count];
			this._declaresName = new Boolean[this._count];

			Int32 current = 0;
			Int32 groups = 0;
			for (Int32 i = 0; i < this._count; i++)
			{
				SslToken token = this._tokens[i];
				if (token.Kind != SslTokenKind.Directive)
				{
					this._frameOf[i] = current;
					continue;
				}

				Int32 end = FindDirectiveEnd(this._tokens, i);
				for (Int32 k = i; k <= end; k++)
				{
					this._frameOf[k] = current;
				}

				switch (this._tokens.GetDirectiveName(i))
				{
					case "define":
						this.ReadDefine(i, end);
						break;
					case "include":
						this.ReadInclude(i, end, this._frames[current].IsLive);
						break;
					case "if":
					case "ifdef":
					case "ifndef":
						groups++;
						Int32 liveBranch = this._tokens.GetDirectiveName(i) == "if" && this.IsZeroCondition(i + 1, end) ? 1 : 0;
						ConditionFrame opened = new ConditionFrame(this._frames.Count, this._frames[current], groups, 0, liveBranch);
						this._frames.Add(opened);
						current = opened.Index;
						break;
					case "else":
					case "elif":
						if (current != 0)
						{
							ConditionFrame previous = this._frames[current];
							ConditionFrame next = new ConditionFrame(this._frames.Count, previous.Parent, previous.Group, previous.Branch + 1, previous.LiveBranch);
							this._frames.Add(next);
							current = next.Index;
						}

						break;
					case "endif":
						if (current != 0)
						{
							current = this._frames[current].Parent?.Index ?? 0;
						}

						break;
					default:
						break;
				}

				i = end;
			}
		}

		private Boolean IsZeroCondition(Int32 first, Int32 last)
		{
			Int32 numbers = 0;
			for (Int32 i = first; i <= last && i < this._count; i++)
			{
				if (this._tokens.IsPunctuation(i, '(') || this._tokens.IsPunctuation(i, ')'))
				{
					continue;
				}

				if (this._tokens[i].Kind != SslTokenKind.Number || !this.Span(i).SequenceEqual("0"))
				{
					return false;
				}

				numbers++;
			}

			return numbers == 1;
		}

		private void ReadDefine(Int32 directive, Int32 end)
		{
			Int32 name = directive + 1;
			if (name > end || this._tokens[name].Kind != SslTokenKind.Identifier)
			{
				return;
			}

			Int32 body = name + 1;
			if (body <= end && this._tokens.IsPunctuation(body, '(') && this._tokens[body].Offset == this._tokens[name].End)
			{
				// A macro with parameters: the body starts after the closing parenthesis.
				Int32 close = body;
				while (close <= end && !this._tokens.IsPunctuation(close, ')'))
				{
					close++;
				}

				body = close + 1;
			}

			System.Text.StringBuilder code = new System.Text.StringBuilder();
			for (Int32 i = body; i <= end && i < this._count; i++)
			{
				if (code.Length > 0)
				{
					code.Append(' ');
				}

				code.Append(this._tokens.GetText(i));
			}

			this._localMacros[this._tokens.GetText(name)] = MacroTraits.From(code.ToString());
		}

		private void ReadInclude(Int32 directive, Int32 end, Boolean live)
		{
			String? path = ReadIncludePath(this._tokens, directive, end);
			if (path is null || !live)
			{
				return;
			}

			this._includes.Add(new IncludeDirective(directive, path));
			this._firstIncludeToken = Math.Min(this._firstIncludeToken, directive);
		}
	}
}