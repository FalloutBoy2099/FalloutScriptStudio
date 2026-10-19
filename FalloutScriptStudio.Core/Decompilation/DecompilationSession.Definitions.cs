namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.Globalization;

	internal sealed partial class DecompilationSession
	{
		/// <summary>
		/// Says what each name of the namespace is: a procedure, an exported variable (with its value), and names the global variables
		/// <c>GVar0</c>, <c>GVar1</c>... until their real names are known.
		/// </summary>
		public void InitializeDefinitions()
		{
			this._definitions.Clear();
			for (Int32 index = 0; index < this._namespace.Count; index++)
			{
				UInt32 nameOffset = this._namespace.GetOffsetByIndex(index);
				Int32 procedureIndex = GetIndexOfProcedure(nameOffset);
				if (procedureIndex != -1)
				{
					this._definitions[nameOffset] = new DefinitionObject(DefinitionObjectType.Procedure, 0, (UInt32)procedureIndex);
					continue;
				}

				Int32 exportedVariableIndex = GetIndexOfExportedVariable(nameOffset);
				if (exportedVariableIndex != -1)
				{
					ScriptOpcode valueOpcode = this._exportedVariableValues[exportedVariableIndex];
					this._definitions[nameOffset] = new DefinitionObject(DefinitionObjectType.Variable, ObjectAttributes.Export | (UInt32)valueOpcode.Operator, valueOpcode.Argument);
				}
			}

			this._globalVariableNames.Clear();
			for (Int32 index = 0; index < this._globalVariables.Count; index++)
			{
				this._globalVariableNames.Add(String.Format(CultureInfo.InvariantCulture, "GVar{0}", index));
			}
		}

		private Int32 GetIndexOfProcedure(String name)
		{
			String lowerName = name.ToLowerInvariant();
			for (UInt32 index = 0; index < this._procedures.Count; index++)
			{
				String testName = this._namespace[this._procedures[index].NameOffset].ToLowerInvariant();
				if (testName == lowerName)
				{
					return (Int32)index;
				}
			}

			return -1;
		}

		private Int32 GetIndexOfProcedure(UInt32 nameOffset)
		{
			for (UInt32 index = 0; index < this._procedures.Count; index++)
			{
				if (this._procedures[index].NameOffset == nameOffset)
				{
					return (Int32)index;
				}
			}

			return -1;
		}

		private Int32 GetIndexOfExportedVariable(UInt32 nameOffset)
		{
			for (Int32 index = 0; index < this._exportedVariableValues.Count; index += 2)
			{
				if (this._exportedVariableValues[index + 1].Argument == nameOffset)
				{
					return index;
				}
			}

			return -1;
		}

		private void SetExternalVariable(UInt32 nameOffset)
		{
			if (!this._definitions.ContainsKey(nameOffset))
			{
				this._definitions[nameOffset] = new DefinitionObject(DefinitionObjectType.Variable, ObjectAttributes.Import, 0);
			}
		}

		/// <summary>
		/// When the names that are left are exactly as many as the global variables, they are those variables' names, in order.
		/// </summary>
		private void TryRenameGlobalVariables()
		{
			Int32 nameCount = this._namespace.Count;
			Int32 definitionCount = this._definitions.Count;
			Int32 globalVariableCount = this._globalVariables.Count;
			Int32 globalVariableIndex = 0;

			if (nameCount - definitionCount == globalVariableCount)
			{
				for (Int32 index = 0; index < this._namespace.Count; index++)
				{
					UInt32 offset = this._namespace.GetOffsetByIndex(index);
					if (!this._definitions.ContainsKey(offset))
					{
						ScriptOpcode globalVariable = this._globalVariables[globalVariableIndex];
						this._definitions[offset] = new DefinitionObject(DefinitionObjectType.Variable, ObjectAttributes.Global | (UInt32)globalVariable.Operator, globalVariable.Argument);
						this._globalVariableNames[globalVariableIndex] = this._namespace.GetStringByIndex(index);
						globalVariableIndex++;
					}
				}
			}
		}

		/// <summary>
		/// A script without global variables of its own: the names that are left are variables it imports.
		/// </summary>
		private void TryRenameImportedVariables()
		{
			if (this._globalVariables.Count != 0)
			{
				return;
			}

			for (Int32 index = 0; index < this._namespace.Count; index++)
			{
				UInt32 offset = this._namespace.GetOffsetByIndex(index);
				if (!this._definitions.ContainsKey(offset))
				{
					this._definitions[offset] = new DefinitionObject(DefinitionObjectType.Variable, ObjectAttributes.Import, 0);
				}
			}
		}
	}
}