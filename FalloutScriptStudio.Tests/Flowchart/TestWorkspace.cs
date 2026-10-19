namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// A workspace stand-in built with a dynamic proxy, so it keeps compiling when members are added to <see cref="IWorkspace"/>.
	/// It parses documents with the internal parser, raises <see cref="IWorkspace.DocumentParsed"/>, renames procedures textually and
	/// records the navigation.
	/// </summary>
	public class TestWorkspace : DispatchProxy
	{
		private EventHandler<DocumentViewModel>? _parsed;

		/// <summary>Gets the recorded calls.</summary>
		public List<String> Calls { get; } = [];

		/// <summary>Gets the recorded navigation targets as file and line.</summary>
		public List<(String? File, Int32 Line)> Navigations { get; } = [];

		/// <summary>Gets the number of parses requested.</summary>
		public Int32 ParseCount { get; private set; }

		/// <summary>Gets or sets a value indicating whether a requested parse produces the program info (<see langword="false"/> simulates a parse that fails or is cancelled).</summary>
		public Boolean ParseSucceeds { get; set; } = true;

		/// <summary>Gets or sets the new name the rename gives, or <see langword="null"/> when the user cancels.</summary>
		public String? RenameTo { get; set; }

		public ObservableCollection<DocumentViewModel> Documents { get; } = [];

		public static (IWorkspace Workspace, TestWorkspace Recorder) Create()
		{
			IWorkspace workspace = DispatchProxy.Create<IWorkspace, TestWorkspace>();
			return (workspace, (TestWorkspace)(Object)workspace);
		}

		/// <summary>Raises <see cref="IWorkspace.DocumentParsed"/> like the parse coordinator does.</summary>
		public void RaiseParsed(DocumentViewModel document)
		{
			this._parsed?.Invoke(this, document);
		}

		protected override Object? Invoke(MethodInfo? targetMethod, Object?[]? args)
		{
			if (targetMethod is null)
			{
				return null;
			}

			Object?[] arguments = args ?? Array.Empty<Object?>();
			switch (targetMethod.Name)
			{
				case "add_DocumentParsed":
					this._parsed += (EventHandler<DocumentViewModel>?)arguments[0];
					return null;
				case "remove_DocumentParsed":
					this._parsed -= (EventHandler<DocumentViewModel>?)arguments[0];
					return null;
				case "get_Documents":
					return this.Documents;
				case "RequestParseAsync":
					return this.Parse((DocumentViewModel)arguments[0]!);
				case "RenameSymbolAsync":
					this.Rename((DocumentViewModel)arguments[0]!, (IScriptSymbol)arguments[1]!);
					return Task.CompletedTask;
				case "GoToAsync":
					this.Navigations.Add(((String?)arguments[0], (Int32)arguments[1]!));
					return Task.CompletedTask;
				default:
					break;
			}

			if (targetMethod.Name.StartsWith("add_", StringComparison.Ordinal) || targetMethod.Name.StartsWith("remove_", StringComparison.Ordinal))
			{
				return null;
			}

			this.Calls.Add(targetMethod.Name);
			Type returnType = targetMethod.ReturnType;
			if (returnType == typeof(Task))
			{
				return Task.CompletedTask;
			}

			if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				Type result = returnType.GetGenericArguments()[0];
				Object? value = result.IsValueType ? Activator.CreateInstance(result) : null;
				return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result).Invoke(null, new[] { value });
			}

			return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
		}

		private Task Parse(DocumentViewModel document)
		{
			this.ParseCount++;
			if (!this.ParseSucceeds)
			{
				return Task.CompletedTask;
			}

			document.ProgramInfo = new InternalParser().Parse(document.TextDocument.Text, document.FilePath);
			document.NeedsParse = false;
			this.RaiseParsed(document);
			return Task.CompletedTask;
		}

		private void Rename(DocumentViewModel document, IScriptSymbol symbol)
		{
			if (this.RenameTo is null)
			{
				return;
			}

			String text = document.TextDocument.Text;
			String pattern = @"\b" + Regex.Escape(symbol.Name) + @"\b";
			List<TextEdit> edits = [];
			foreach (Match match in Regex.Matches(text, pattern))
			{
				edits.Add(new TextEdit(match.Index, match.Length, this.RenameTo));
			}

			DocumentEdits.Apply(document.TextDocument, edits);
		}
	}
}