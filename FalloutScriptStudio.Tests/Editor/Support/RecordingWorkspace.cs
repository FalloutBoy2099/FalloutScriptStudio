namespace FalloutScriptStudio.Tests.Editor.Support
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Reflection;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;

	/// <summary>
	/// A stand-in for <see cref="IWorkspace"/> built with a dynamic proxy: every call is recorded and returns a harmless
	/// default, so the tests keep compiling when members are added to the interface.
	/// </summary>
	public class RecordingWorkspace : DispatchProxy
	{
		/// <summary>Gets the recorded calls as <c>MethodName(argument, ...)</c> strings.</summary>
		public List<String> Calls { get; } = [];

		/// <summary>Gets the names that <see cref="IWorkspace.HasProjectSymbol"/> reports as known to the project index.</summary>
		public HashSet<String> ProjectSymbols { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Gets the texts of the open documents by full path, returned by <see cref="IWorkspace.GetOpenDocumentTexts"/>.</summary>
		public Dictionary<String, String> OpenTexts { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Gets the texts returned by <see cref="IWorkspace.GetMessageText"/>.</summary>
		public Dictionary<Int32, String> Messages { get; } = [];

		/// <summary>Gets or sets the snippet that <see cref="IWorkspace.PickSnippetAsync"/> chooses; <see langword="null"/> cancels the list.</summary>
		public FalloutScriptStudio.Core.Snippets.Snippet? SnippetAnswer { get; set; }

		/// <summary>Gets the lists of snippets the editor asked to show.</summary>
		public List<FalloutScriptStudio.Services.ToolWindows.SnippetPickerRequest> SnippetRequests { get; } = [];

		/// <summary>Creates a workspace stand-in.</summary>
		/// <returns>The workspace and its recorder.</returns>
		public static (IWorkspace Workspace, RecordingWorkspace Recorder) Create()
		{
			IWorkspace workspace = DispatchProxy.Create<IWorkspace, RecordingWorkspace>();
			return (workspace, (RecordingWorkspace)(Object)workspace);
		}

		/// <inheritdoc />
		protected override Object? Invoke(MethodInfo? targetMethod, Object?[]? args)
		{
			if (targetMethod is null)
			{
				return null;
			}

			String name = targetMethod.Name;
			if (name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal))
			{
				return null;
			}

			this.Calls.Add(name + "(" + String.Join(", ", args ?? Array.Empty<Object?>()) + ")");
			if (name == nameof(IWorkspace.PickSnippetAsync) && args is [FalloutScriptStudio.Services.ToolWindows.SnippetPickerRequest request])
			{
				this.SnippetRequests.Add(request);
				return Task.FromResult(this.SnippetAnswer);
			}

			if (name == nameof(IWorkspace.HasProjectSymbol) && args is [String symbol])
			{
				return this.ProjectSymbols.Contains(symbol);
			}

			if (name == nameof(IWorkspace.GetMessageText) && args is [_, Int32 id])
			{
				return this.Messages.TryGetValue(id, out String? text) ? text : null;
			}

			if (name == nameof(IWorkspace.GetOpenDocumentTexts))
			{
				return this.OpenTexts;
			}

			Type returnType = targetMethod.ReturnType;
			if (returnType == typeof(Task))
			{
				return Task.CompletedTask;
			}

			if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				Type result = returnType.GetGenericArguments()[0];
				Object? value = result == typeof(Boolean) ? true : result.IsValueType ? Activator.CreateInstance(result) : null;
				return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result).Invoke(null, new[] { value });
			}

			if (returnType == typeof(ObservableCollection<DocumentViewModel>))
			{
				return new ObservableCollection<DocumentViewModel>();
			}

			if (returnType == typeof(IReadOnlyDictionary<String, String>))
			{
				return new Dictionary<String, String>();
			}

			return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
		}
	}
}