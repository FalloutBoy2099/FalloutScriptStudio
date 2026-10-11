namespace FalloutScriptStudio.Core.Functions
{
	using System;

	/// <summary>
	/// Outcome of an edit of the functions tree.
	/// </summary>
	/// <param name="Error">The reason of the failure, or <see cref="FunctionOperationError.None"/>.</param>
	/// <param name="Message">A description of the failure, or <c>null</c> on success.</param>
	public sealed record FunctionOperationResult(FunctionOperationError Error, String? Message)
	{
		/// <summary>Gets the successful result.</summary>
		public static FunctionOperationResult Success { get; } = new FunctionOperationResult(FunctionOperationError.None, null);

		/// <summary>Gets a value indicating whether the operation was done.</summary>
		public Boolean Succeeded => this.Error == FunctionOperationError.None;

		/// <summary>Creates a failed result.</summary>
		/// <param name="error">The reason.</param>
		/// <param name="message">The description.</param>
		/// <returns>The result.</returns>
		public static FunctionOperationResult Fail(FunctionOperationError error, String message)
		{
			return new FunctionOperationResult(error, message);
		}
	}
}
