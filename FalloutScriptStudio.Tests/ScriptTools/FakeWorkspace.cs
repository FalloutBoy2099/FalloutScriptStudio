namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Reflection;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// A workspace stand-in that records navigation and search results. <see cref="Interface"/> is a dynamic proxy so that this
	/// class keeps compiling when members are added to <see cref="IWorkspace"/>; members it does not know do nothing.
	/// </summary>
	internal sealed class FakeWorkspace
	{
		public FakeWorkspace()
		{
			this.Interface = DispatchProxy.Create<IWorkspace, WorkspaceProxy>();
			((WorkspaceProxy)(Object)this.Interface).Target = this;
		}

		public IWorkspace Interface { get; }

		public ObservableCollection<DocumentViewModel> Documents { get; } = [];

		public DocumentViewModel? ActiveDocument { get; set; }

		public List<String> Opened { get; } = [];

		public List<(String? File, Int32 Line, Int32 Column, Int32 Length)> Navigations { get; } = [];

		public List<IReadOnlyList<BuildError>> SearchResults { get; } = [];

		public List<String> Statuses { get; } = [];

		public DocumentViewModel AddDocument(String? path, String text)
		{
			DocumentViewModel document = new DocumentViewModel(path, DocumentViewModel.KindFromPath(path), new TextDocument(text));
			document.Editor = FakeEditorSurface.Create(document.TextDocument);
			this.Documents.Add(document);
			this.ActiveDocument ??= document;
			return document;
		}

		private DocumentViewModel? Find(String filePath)
		{
			return this.Documents.FirstOrDefault(document => String.Equals(document.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
		}

		private Object? Handle(MethodInfo method, Object?[] args)
		{
			switch (method.Name)
			{
				case "get_Documents":
					return this.Documents;
				case "get_ActiveDocument":
					return this.ActiveDocument;
				case "set_ActiveDocument":
					this.ActiveDocument = (DocumentViewModel?)args[0];
					return null;
				case "FindDocument":
					return this.Find((String)args[0]!);
				case "OpenAsync":
					this.Opened.Add((String)args[0]!);
					return Task.FromResult(this.Find((String)args[0]!));
				case "GoToAsync":
					this.GoTo((String?)args[0], (Int32)args[1]!, (Int32)args[2]!, (Int32)args[3]!);
					return Task.CompletedTask;
				case "ShowSearchResults":
					this.SearchResults.Add((IReadOnlyList<BuildError>)args[0]!);
					return null;
				case "GetOpenDocumentTexts":
					return this.Documents
						.Where(document => document.FilePath is not null)
						.ToDictionary(document => document.FilePath!, document => document.TextDocument.Text, StringComparer.OrdinalIgnoreCase);
				case "ReportStatus":
					this.Statuses.Add((String)args[0]!);
					return null;
				default:
					return DefaultValue(method.ReturnType);
			}
		}

		private void GoTo(String? file, Int32 line, Int32 column, Int32 length)
		{
			this.Navigations.Add((file, line, column, length));
			DocumentViewModel? target = file is null ? this.ActiveDocument : this.Find(file);
			if (target is not null)
			{
				this.ActiveDocument = target;
				target.Editor?.GoTo(line, column, length);
			}
		}

		private static Object? DefaultValue(Type type)
		{
			if (type == typeof(void))
			{
				return null;
			}

			if (type == typeof(Task))
			{
				return Task.CompletedTask;
			}

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
			{
				Type result = type.GetGenericArguments()[0];
				Object? value = result.IsValueType ? Activator.CreateInstance(result) : null;
				return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result).Invoke(null, new[] { value });
			}

			return type.IsValueType ? Activator.CreateInstance(type) : null;
		}

		public class WorkspaceProxy : DispatchProxy
		{
			public FakeWorkspace? Target { get; set; }

			protected override Object? Invoke(MethodInfo? targetMethod, Object?[]? args)
			{
				return this.Target!.Handle(targetMethod!, args ?? Array.Empty<Object?>());
			}
		}
	}
}