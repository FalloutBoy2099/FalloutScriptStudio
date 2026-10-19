namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	/// <summary>
	/// Collects <see cref="ParseDiagnostic"/> messages produced during one parse operation.
	/// </summary>
	/// <remarks>Instances are not thread-safe; create one per parse operation.</remarks>
	public sealed class ParseDiagnostics : IReadOnlyList<ParseDiagnostic>
	{
		private readonly List<ParseDiagnostic> _items = [];

		/// <inheritdoc />
		public Int32 Count => this._items.Count;

		/// <inheritdoc />
		public ParseDiagnostic this[Int32 index] => this._items[index];

		/// <summary>Adds a message.</summary>
		/// <param name="diagnostic">The message to add.</param>
		public void Add(ParseDiagnostic diagnostic)
		{
			this._items.Add(diagnostic ?? throw new ArgumentNullException(nameof(diagnostic)));
		}

		/// <summary>Adds a message.</summary>
		/// <param name="severity">Severity of the message.</param>
		/// <param name="source">Component that produced the message.</param>
		/// <param name="line">1-based line, or -1.</param>
		/// <param name="message">Message text.</param>
		public void Add(ParseDiagnosticSeverity severity, String source, Int32 line, String message)
		{
			this._items.Add(new ParseDiagnostic(severity, source, line, message));
		}

		/// <summary>Adds all messages of another collection.</summary>
		/// <param name="other">The messages to add.</param>
		public void AddRange(IEnumerable<ParseDiagnostic> other)
		{
			this._items.AddRange(other);
		}

		/// <summary>Formats the messages, one per line, in the style of the legacy parser log.</summary>
		/// <param name="minimumSeverity">Messages less severe than this are left out.</param>
		/// <returns>The log text; empty when there are no messages.</returns>
		public String ToLogText(ParseDiagnosticSeverity minimumSeverity = ParseDiagnosticSeverity.Info)
		{
			var sb = new StringBuilder();
			foreach (ParseDiagnostic item in this._items.Where(d => d.Severity >= minimumSeverity))
			{
				sb.Append(item).Append("\r\n");
			}

			return sb.ToString();
		}

		/// <inheritdoc />
		public IEnumerator<ParseDiagnostic> GetEnumerator()
		{
			return this._items.GetEnumerator();
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}
}