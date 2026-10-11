namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Reflection;
	using FalloutScriptStudio.Documents;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// A text editor stand-in backed by a document, with a selection and a caret. It is a dynamic proxy so that it keeps compiling
	/// when members are added to <see cref="IEditorSurface"/>; members it does not know return default values.
	/// </summary>
	internal sealed class FakeEditorSurface
	{
		private readonly TextDocument _document;

		private FakeEditorSurface(TextDocument document)
		{
			this._document = document;
		}

		private Int32 CaretOffset { get; set; }

		private Int32 SelectionStart { get; set; }

		private Int32 SelectionLength { get; set; }

		public static IEditorSurface Create(TextDocument document)
		{
			IEditorSurface surface = DispatchProxy.Create<IEditorSurface, EditorProxy>();
			((EditorProxy)(Object)surface).Target = new FakeEditorSurface(document);
			return surface;
		}

		private Object? Handle(MethodInfo method, Object?[] args)
		{
			switch (method.Name)
			{
				case "get_CaretOffset":
					return this.CaretOffset;
				case "set_CaretOffset":
					this.CaretOffset = (Int32)args[0]!;
					return null;
				case "get_CaretLine":
					return this._document.GetLocation(this.CaretOffset).Line;
				case "get_CaretColumn":
					return this._document.GetLocation(this.CaretOffset).Column;
				case "get_SelectionStart":
					return this.SelectionStart;
				case "get_SelectionLength":
					return this.SelectionLength;
				case "get_SelectedText":
					return this._document.GetText(this.SelectionStart, this.SelectionLength);
				case "Select":
					this.Select((Int32)args[0]!, (Int32)args[1]!);
					return null;
				case "SelectAll":
					this.Select(0, this._document.TextLength);
					return null;
				case "GoTo":
					this.Select(this._document.GetOffset((Int32)args[0]!, Math.Max((Int32)args[1]!, 1)), Math.Max((Int32)args[2]!, 0));
					return null;
				case "ReplaceSelection":
					this._document.Replace(this.SelectionStart, this.SelectionLength, (String)args[0]!);
					this.Select(this.SelectionStart + ((String)args[0]!).Length, 0);
					return null;
				case "InsertText":
					this._document.Insert(this.CaretOffset, (String)args[0]!);
					return null;
				default:
					return method.ReturnType.IsValueType && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
			}
		}

		private void Select(Int32 offset, Int32 length)
		{
			this.SelectionStart = offset;
			this.SelectionLength = length;
			this.CaretOffset = offset + length;
		}

		public class EditorProxy : DispatchProxy
		{
			public FakeEditorSurface? Target { get; set; }

			protected override Object? Invoke(MethodInfo? targetMethod, Object?[]? args)
			{
				return this.Target!.Handle(targetMethod!, args ?? Array.Empty<Object?>());
			}
		}
	}
}
