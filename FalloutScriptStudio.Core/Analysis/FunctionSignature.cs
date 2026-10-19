namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>The signature of an engine or sfall function as the description files state it (<c>opcodes.txt</c> and <c>functions.ini</c>).</summary>
	public sealed class FunctionSignature
	{
		/// <summary>Initializes a new instance of the <see cref="FunctionSignature"/> class.</summary>
		/// <param name="name">The name of the function.</param>
		/// <param name="returnType">The type of the result, or an empty text when the description states none.</param>
		/// <param name="parameters">The parameters in order.</param>
		/// <param name="isDeprecated">Whether the description marks the function as deprecated.</param>
		public FunctionSignature(String name, String returnType, IReadOnlyList<FunctionParameter> parameters, Boolean isDeprecated)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(returnType);
			ArgumentNullException.ThrowIfNull(parameters);
			this.Name = name;
			this.ReturnType = returnType;
			this.Parameters = parameters;
			this.IsDeprecated = isDeprecated;

			Int32 required = 0;
			Boolean variadic = false;
			foreach (FunctionParameter parameter in parameters)
			{
				if (parameter.IsVariadic)
				{
					variadic = true;
				}
				else if (!parameter.IsOptional)
				{
					required++;
				}
			}

			this.MinArguments = required;
			this.MaxArguments = variadic ? Int32.MaxValue : parameters.Count;
		}

		/// <summary>Gets the name of the function.</summary>
		public String Name { get; }

		/// <summary>Gets the type of the result, or an empty text.</summary>
		public String ReturnType { get; }

		/// <summary>Gets the parameters in order.</summary>
		public IReadOnlyList<FunctionParameter> Parameters { get; }

		/// <summary>Gets a value indicating whether the description marks the function as deprecated or obsolete.</summary>
		public Boolean IsDeprecated { get; }

		/// <summary>Gets the fewest arguments a call needs.</summary>
		public Int32 MinArguments { get; }

		/// <summary>Gets the most arguments a call takes (<see cref="Int32.MaxValue"/> when the description ends with <c>...</c>).</summary>
		public Int32 MaxArguments { get; }

		/// <summary>Gets a value indicating whether the function is called with parentheses: a function without parameters is written without them (<c>dude_obj</c>, never <c>dude_obj()</c>).</summary>
		public Boolean TakesParentheses => this.Parameters.Count > 0;

		/// <summary>Determines whether a call with some arguments matches the signature.</summary>
		/// <param name="argumentCount">The number of arguments.</param>
		/// <returns><see langword="true"/> when the count is allowed.</returns>
		public Boolean Accepts(Int32 argumentCount)
		{
			return argumentCount >= this.MinArguments && argumentCount <= this.MaxArguments;
		}
	}
}