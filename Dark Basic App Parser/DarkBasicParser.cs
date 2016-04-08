using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static Dark_Basic_App_Parser.ErrorList;
using static Dark_Basic_App_Parser.Helper;

namespace Dark_Basic_App_Parser {
	public class DarkBasicParser {
		public List<FunctionOrSubroutine> subroutinesAndFunctions = new List<FunctionOrSubroutine>();
		public List<Variable> variables = new List<Variable>();

		public string badWordPattern;
		private readonly string[] nonVariableCharacters = { " ", "(", ")", ",", "=", "*", "%", "/", "+", "-", ">=", ">", "<", "<=" };
		private readonly List<string> constructDeclarationWords = new List<string> { "function", "endfunction", ":", "return" };

		public DarkBasicParser(List<string> badList) {
			//Used later on with Regex to find and remove any bad words (Aka DarkBasic functions). Temporarily,
			//to avoid adding them incorrectly as variables.
			badWordPattern = string.Join("|", badList.Select(f => @"\b" + Regex.Escape(f) + @"\b"));
		}

		public List<string> GetIncludedFiles(string file) {
			var includedFiles = new List<string>();
			using(var sr = new StreamReader(file)) {
				var lines = sr.ReadToEnd().Split(new[] { "\r\n" }, StringSplitOptions.None);
				for(int i = 0; i < lines.Length; i++) {
					var lineContent = lines[i];
					if(lineContent.StartsWith("#include ")) {
						var filename = lineContent.Split(' ')[1].Replace("\"", null);
						if(!filename.Contains("\\")) {
							//There's no path to it, it's local.
							var correctedFileLocation = Environment.CurrentDirectory + @"\" + filename;
							if(!File.Exists(correctedFileLocation)) {
								throw new FileNotFoundException(INCLUDE_FILE_NOT_FOUND, correctedFileLocation);
							}
							includedFiles.Add(correctedFileLocation);
						} else {
							if(!File.Exists(filename)) {
								throw new FileNotFoundException(INCLUDE_FILE_NOT_FOUND, filename);
							}
							includedFiles.Add(filename);
						}
					}
				}
			}
			return includedFiles;
		}

		public string ConvertToLocalPath(string file) {
			if(File.Exists(file)) {
				var fl = new FileInfo(file);
				if(fl.DirectoryName != null) {
					var temp = file.Remove(0, fl.DirectoryName.Length);
					//var start = file.IndexOf(fl.DirectoryName, StringComparison.Ordinal);
					//return file.Substring(start, file.Length - start);
					return temp;
				}
			}

			return file;
		}

		public bool IsVariable(string var, List<FunctionOrSubroutine> subroutinesAndFunctionsList) {
			return var.Any(char.IsLetter) && !var.Contains("\"") && !var.Contains(":") && subroutinesAndFunctionsList.All(f => f.Name != var);
		}

		public string ParseFunctionAndSubroutine(string lineContent, int curLine, string localFile) {
			if(constructDeclarationWords.Any(f => lineContent.ToLower().Contains(f)) && !lineContent.Contains("`")) {
				if(lineContent.Contains(":") && !lineContent.Contains(" ")) {
					//Subroutine start
					var subroutineName = lineContent.Substring(0, lineContent.IndexOf(":", StringComparison.Ordinal));
					subroutinesAndFunctions.Add(new FunctionOrSubroutine {
						Name = subroutineName,
						TypeOfConstruct = ConstructType.Subroutine,
						LineDeclaredAt = new List<int> { curLine },
						LinesUsedOn = new List<int>(),
						File = localFile
					});
					return "Found subroutine start on line " + curLine + " in " + localFile;
				}
				if(lineContent.StartsWith("return")) {
					//End subroutine
					subroutinesAndFunctions[subroutinesAndFunctions.Count - 1].LineDeclaredAt.Add(curLine);
					return "Found subroutine end on line " + curLine + " in " + localFile;
				}
				if(lineContent.ToLower().StartsWith("function ") && !lineContent.ToLower().Contains("endfunction")) {
					//Function start
					var functionName = GetStringBetween(lineContent, "function ", "(").Replace("unction ", "");
					var parameters = GetStringBetween(lineContent, "(", ")").Split(',');
					subroutinesAndFunctions.Add(new FunctionOrSubroutine {
						Name = functionName,
						TypeOfConstruct = ConstructType.Function,
						Parameters = parameters.ToList(),
						LineDeclaredAt = new List<int> { curLine },
						LinesUsedOn = new List<int>(),
						File = localFile
					});
					return "Found function start on line " + curLine + " in " + localFile;
				}
				if(lineContent.ToLower().StartsWith("endfunction")) {
					//End function
					subroutinesAndFunctions[subroutinesAndFunctions.Count - 1].LineDeclaredAt.Add(curLine);
					subroutinesAndFunctions[subroutinesAndFunctions.Count - 1].ReturnValue = lineContent.Replace("endfunction", "");
					return "Found function end on line " + curLine + " in " + localFile;
				}
			}
			return null;
		}

		public void DealWithVariableAndSubroutineAppropriately(string lineContent, int curLine, List<string> removableVariableWords, string localFile) {
			var varList = ValidateAndParseVariable(lineContent, curLine, removableVariableWords, subroutinesAndFunctions, localFile);
			foreach(var variable in varList) {
				if(variable.Name != null) {
					//Dealing with a variable of some kind
					AddVariable(variable, curLine);
				}
			}

			//Ensure it is actually a construct of some kind
			if(!lineContent.Contains("`")) {
				//We're possibly dealing with a function/subroutine
				if(lineContent.Contains("gosub ")) {
					//Calling subroutine
					var subName = lineContent.Replace("gosub ", "");
					AddMethodUsageLine(ref subroutinesAndFunctions, subName, curLine);
				} else if(subroutinesAndFunctions.Any(f => lineContent.Contains(f.Name + "(")) && !lineContent.Contains("function ")) {
					//Calling function
					AddMethodUsageLine(ref subroutinesAndFunctions, lineContent, curLine);
				}
			}
		}

		//Add variable to the variables list, after it has been checked that it is actually a variable
		public void AddVariable(Variable variable, int curLine) {
			foreach(var construct in subroutinesAndFunctions) {
				//Go through every construct
				//Check if potential variable is being used inside of that construct
				if(construct.LineDeclaredAt.Count > 0) {
					//We're inside of a function and have been declared, we are in local scope
					//>= and <= because there could be parameters involved, which should be declared local
					if(curLine >= construct.LineDeclaredAt[0] && curLine <= construct.LineDeclaredAt[1]) {
						variable.Scope = Scope.Local;
						break;
					}
				}
			}

			variable.Name = variable.Name.Trim();

			//var typeOfData = VariableType.Int;

			//if(isArray) {
			//	switch(GetTypeOfData(varParts[0].Trim())) {
			//		case VariableType.Float:
			//			typeOfData = VariableType.Array_of_Floats;
			//		break;

			//		case VariableType.Int:
			//			typeOfData = VariableType.Array_of_Ints;
			//		break;

			//		case VariableType.String:
			//			typeOfData = VariableType.Array_of_Strings;
			//		break;
			//	}
			//} else {
			//	typeOfData = GetTypeOfData(varParts[0].Trim());
			//}

			var index = variables.FindIndex(f => f.Name == variable.Name);
			if(index == -1) {
				//Not in list, add it
				variables.Add(variable);
			} else {
				//Already in list, so we're using it's value (in DB)
				variables[index].LinesUsedOn.Add(curLine);
			}
		}

		public List<Construct> ParseFunctionCall(string call, int curLine, List<FunctionOrSubroutine> functionsAndSubroutines) {
			var constructs = new List<Construct>();
			if(call.Contains("(") && call.Contains(")")) { //&& !call.StartsWith("dim")
				int currentFuncCount = 0;
				var funcStartPos = new List<int>();
				var funcEndPos = new List<int>();
				for(int index = 0; index < call.Length; index++) {
					var character = call[index];
					if(character == '(') {
						currentFuncCount += 1;
						if(call.StartsWith("dim")) {
							funcStartPos.Add(3);
						} else {
							funcStartPos.Add(index);
						}
					} else if(character == ')') {
						currentFuncCount -= 1;
						funcEndPos.Add(index);
					}
				}

				//There's no equal amount of brackets, there's a syntax error present
				if(currentFuncCount != 0) {
					Console.WriteLine("There is a syntax error in the program. Line: " + curLine);
				}

				var tmpLine = call;
				for(int i = 0; i < funcStartPos.Count; i++) {
					string parameters = null; //call.Substring(call.IndexOf("(", StringComparison.Ordinal));
					if(tmpLine.StartsWith("dim")) {
						parameters = GetStringBetween(tmpLine, "(", ")"); //tmpLine.Substring(funcStartPos[i], funcEndPos[i] - funcStartPos[i]);
					} else {
						parameters = call.Substring(funcStartPos[i] + 1, funcEndPos[i] - funcStartPos[i] - 1);
					}

					constructs.Add(GetConstruct(tmpLine, parameters));
				}
			}

			return constructs;
		}

		private Construct GetConstruct(string currentLine, string parameters) {
			var construct = new Construct();
			construct.Parameters = parameters.Split(new[] { "(", ",", ")" }, StringSplitOptions.RemoveEmptyEntries).ToList();
			construct.Name = currentLine.Substring(0, currentLine.IndexOf("(", StringComparison.Ordinal));
			if(construct.Name != null) {
				if(construct.Name.StartsWith("dim")) {
					construct.Name = construct.Name.Remove(0, 3).Trim();
					construct.ConstructType = ConstructType.Array;
					//Remove call to array, which would be the entirety of the string
					currentLine = currentLine.Remove(0, currentLine.Length);
				} else {
					construct.ConstructType = ConstructType.Function;
					//Remove the function name and its parameters from the call
					currentLine = currentLine.Replace(construct.Name + "(" + parameters + ")", null);
				}
			}
			return construct;
		}

		public List<Variable> ValidateAndParseVariable(string currentLine, int curLine, List<string> removableVariableWords, List<FunctionOrSubroutine> subroutinesAndFunctionsList, string localFile) {
			currentLine = currentLine.Trim();
			string variableName = null;
			string variableValue = null;
			var dataType = VariableType.Unknown;

			if(currentLine.Contains("`")) { return new List<Variable>(); }

			//var possibleVars = new List<string>();
			//if(currentLine.Contains("(") && currentLine.Contains(")")) {
			//	var tmpList = GetStringBetween(currentLine, "(", ")");
			//	//if(tm)
			//             //possibleVars.AddRange();
			//	//Remove any darkbasic function calls
			//	possibleVars.AddRange(currentLine.Split(new[] { " and ", " to ", " or ", "+", "-", "<", "<=", ">", ">=" }, StringSplitOptions.RemoveEmptyEntries));
			//} else {
			//Remove any darkbasic function calls
			//possibleVars.AddRange(currentLine.Split(new[] { "(", ")", " and ", " to ", " or ", "/", "*", "+", "-", "<", "<=", ">", ">=", "," }, StringSplitOptions.RemoveEmptyEntries));
			//}

			//foreach(string word in potentialVariable.Split(' ').ToString().Trim()) {
			//	if(!string.IsNullOrEmpty(word) && nonVariableWords.Any(f => f.StartsWith(word))) {
			//		return new List<Variable>() { new Variable() };
			//	}
			//}

			var varList = new List<Variable>();
			//foreach(var potentialVariable in possibleVars) {
			var tmpVar = currentLine;

			tmpVar = RemoveBadWords(currentLine, curLine, subroutinesAndFunctionsList);
			//Console.WriteLine("Removed bad words for line " + curLine);

			//Declaration of a variable
			var words = tmpVar.Split(new[] { " ", ",", ")", "(", "+", "-", "*", "/", "%", "=", "<=", "<", ">", ">=" }, StringSplitOptions.RemoveEmptyEntries);
			if(words.Length >= 3) {
				//Check if second word used is an equals sign, represents the declaration of a new variable
				if(words[1] == "=") {
					//Add first variable, which is being declared.
					varList.Add(new Variable {
						Name = words[0],
						LineDeclaredAt = curLine,
						TypeOfData = GetTypeOfData(words[0]),
						LinesUsedOn = new List<int>(),
						Scope = Scope.Global,
						File = localFile
					});

					//Check if any other variables are used when declaring the first
					for(int i = 2; i < words.Length; i++) {
						if(IsVariable(words[i], subroutinesAndFunctionsList)) {
							varList.Add(new Variable {
								Name = words[i],
								LineDeclaredAt = curLine,
								TypeOfData = GetTypeOfData(words[i]),
								LinesUsedOn = new List<int>(),
								Scope = Scope.Global,
								File = localFile
							});
						}
					}

					return varList;
				}
			}

			if(currentLine.StartsWith("dim")) {
				//Array declaration
				var constructs = ParseFunctionCall(tmpVar, curLine, subroutinesAndFunctionsList);
				foreach(var construct in constructs) {
					//var arrName = GetStringBetween(tmpVar, "dim ", "(");
					varList.Add(new Variable {
						Name = construct.Name,
						LineDeclaredAt = curLine,
						TypeOfData = GetTypeOfData(construct.Name, true),
						LinesUsedOn = new List<int>(),
						Scope = Scope.Global,
						File = localFile
					});

					//Add all the parameters, if they are actually variables
					foreach(var variable in construct.Parameters) {
						if(IsVariable(variable, subroutinesAndFunctionsList)) {
							varList.Add(new Variable {
								Name = variable,
								LineDeclaredAt = curLine,
								TypeOfData = GetTypeOfData(variable),
								LinesUsedOn = new List<int>(),
								Scope = Scope.Global,
								File = localFile
							});
						}
					}
				}

				return varList;
			}

			//Check for variables being used
			if(currentLine.StartsWith("if")) {
				var lst = new List<string>();

				//Check inside of any function calls for parameters being sent over
				var constructs = ParseFunctionCall(tmpVar, curLine, subroutinesAndFunctionsList);
				foreach(var construct in constructs) {
					if(construct.Parameters != null) {
						lst.AddRange(construct.Parameters);
					}

					if(construct.Name != null) {
						lst.AddRange(construct.Name.Split(new[] { " ", "and", "+", "-", "*", "/", "%", "=", ">", ">=", "<=", "<" }, StringSplitOptions.RemoveEmptyEntries));
					} else {
						//
						lst.AddRange(tmpVar.Split(new[] { " ", "(", ")", ",", "and", "+", "-", "*", "/", "%", "=", ">", ">=", "<=", "<" }, StringSplitOptions.RemoveEmptyEntries));
					}

					foreach(var itm in lst) {
						if(IsVariable(itm, subroutinesAndFunctionsList)) {
							varList.Add(new Variable {
								Name = itm,
								LineDeclaredAt = curLine,
								TypeOfData = GetTypeOfData(itm),
								LinesUsedOn = new List<int>(),
								Scope = Scope.Global,
								File = localFile
							});
						}
					}
				}
				return varList;
			}
			foreach(var itm in words) {
				if(IsVariable(itm, subroutinesAndFunctionsList)) {
					varList.Add(new Variable {
						Name = itm,
						LineDeclaredAt = curLine,
						TypeOfData = GetTypeOfData(itm),
						LinesUsedOn = new List<int>(),
						Scope = Scope.Global,
						File = localFile
					});
				}
			}
			return varList;

			//var dataInsideFunctionCall = GetStringBetween(tmpVar, "(", ")") != "";
			//if(tmpVar.StartsWith("for")) {
			//	foreach(var itm in tmpVar.Split(new[] { "for", "to" }, StringSplitOptions.RemoveEmptyEntries)) {
			//		variableName = GetStringBetween(itm, "for ", " ");
			//		variableValue = GetStringBetween(itm, variableName + " =", " to");
			//		varList.Add(new Variable {
			//			Name = variableName,
			//			LineDeclaredAt = curLine,
			//			TypeOfData = dataType,
			//			LinesUsedOn = new List<int>(),
			//			Scope = Scope.Global
			//		});
			//	}
			//} else if(tmpVar.Contains("(") && tmpVar.Contains(")") && tmpVar.Contains(",")) {
			//	//We're editing an array's contents
			//	foreach(var itm in tmpVar.Split(new[] { "(", ")", ",", "+", "-", "*", "/" }, StringSplitOptions.RemoveEmptyEntries)) {
			//		variableName = itm;
			//		if(IsVariable(variableName)) {
			//			varList.Add(new Variable {
			//				Name = variableName,
			//				LineDeclaredAt = curLine,
			//				TypeOfData = dataType,
			//				LinesUsedOn = new List<int>(),
			//				Scope = Scope.Global
			//			});
			//		}
			//	}
			//	variableName = tmpVar.Substring(0, tmpVar.IndexOf("(", StringComparison.Ordinal));
			//} else if(tmpVar.Contains("(") && tmpVar.Contains(")") && dataInsideFunctionCall) {
			//	//Variable is being passed as a paremeter inside of function
			//	variableName = GetStringBetween(tmpVar, "(", ")");
			//	foreach(var itm in tmpVar.Split(new[] { "(", ")", ",", "+", "-", "*", "/" }, StringSplitOptions.RemoveEmptyEntries)) {
			//		variableName = itm;
			//		if(IsVariable(variableName)) {
			//			varList.Add(new Variable {
			//				Name = variableName,
			//				LineDeclaredAt = curLine,
			//				TypeOfData = dataType,
			//				LinesUsedOn = new List<int>(),
			//				Scope = Scope.Global
			//			});
			//		}
			//	}
			//} else if(tmpVar.StartsWith("until")) {
			//	//Variable used in repeat-until statement
			//	variableName = GetStringBetween(tmpVar, "until ", " ");
			//} else if(tmpVar.StartsWith("while")) {
			//	//Variable used in while statement
			//	variableName = GetStringBetween(tmpVar, "while ", " ");
			//} else if(tmpVar.StartsWith("dim")) {
			//	//Declaring array of some type
			//	variableName = GetStringBetween(tmpVar, "dim ", "(");
			//	dataType = GetTypeOfData(variableName, true);
			//}

			//}else if(tmpVar.Any(char.IsLetter)) {
			//	variableName = tmpVar;
			//	dataType = GetTypeOfData(variableName, false);
			//}

			/*

			if(dataType == VariableType.Unknown) {
				dataType = GetTypeOfData(tmpVar);
			}

			
			if(!string.IsNullOrEmpty(variableName)) {
				varList.Add(new Variable {
					Name = variableName,
					LineDeclaredAt = curLine,
					TypeOfData = dataType,
					LinesUsedOn = new List<int>(),
					Scope = Scope.Global
				});
			}
			//}
			*/

			//return varList;
		}

		public bool VariableContainsInvalidCharacters(string potentialVariable) {
			var badCharacters = new List<string> { "`" };
			if(badCharacters.Any(f => potentialVariable.StartsWith(f))) {
				return true;
			}

			return false;
		}

		public string RemoveBadWords(string line, int curLine, List<FunctionOrSubroutine> functionsAndSubroutinesList) {
			//Remove any function calls that aren't ours
			var constructs = ParseFunctionCall(line, curLine, functionsAndSubroutinesList);
			var potentialBadWords = new List<string>();

			//Remove any non-useful function calls (those built into dark basic)
			potentialBadWords.AddRange(line.Split(nonVariableCharacters, StringSplitOptions.RemoveEmptyEntries));

			foreach(var construct in constructs) {
				string replacement = null;
				//Remove any function calls that aren't set in the code
				if(construct.ConstructType == ConstructType.Function) {
					if(construct.Name != null) {
						foreach(var word in construct.Name.Split(' ')) {
							if(word.Contains("(")) {
								//Calling a function
								var funcName = word.Substring(0, word.IndexOf("(", StringComparison.Ordinal));
								if(functionsAndSubroutinesList.Any(f => f.Name == funcName)) {
									//Calling a function that we have declared, so we can add it.
									replacement += word + " ";
								}
							} else {
								//It's a variable
								replacement += word + " ";
							}
						}

						line = replacement;
					}
				}

				//Add any parameters into the potential bad list, if there were any parsed parameters found and the list doesn't already have them
				if(construct.Parameters != null) {
					foreach(var param in construct.Parameters) {
						if(!potentialBadWords.Contains(param)) {
							potentialBadWords.Add(param);
						}
					}
				}
			}

			if(line != null) {
				//http://stackoverflow.com/questions/13024073/regex-c-sharp-extract-text-within-double-quotes - Thanks!
				//Remove any strings, otherwise every word inside of the string will be interpreted as a variable
				foreach(var match in Regex.Matches(line, "\"[^\"]*\"")) {
					//We found a string! - Time to remove it
					line = line.Replace(match.ToString(), null);
				}

				//foreach(var badString in badList) {
				//var badWords = badString.Split(' ');
				//if(line.Contains(badString)) {
				//	line = CheckForBadWords(badWords, potentialBadWords, line);
				//}
				//}
				var tmpLine = line;
				Array.ForEach(nonVariableCharacters, f => tmpLine = tmpLine.Replace(f, " "));
				var regex = Regex.Matches(tmpLine, badWordPattern);
				foreach(var match in regex) {
					var startIndex = line.IndexOf(match.ToString(), StringComparison.Ordinal);
					line = line.Remove(startIndex, match.ToString().Length);
				}
			}

			return line;
		}

		public static VariableType GetTypeOfData(string variableName, bool isArray = false) {
			if(variableName == null) { return VariableType.Unknown; };

			if(variableName.Contains("#")) {
				if(isArray) {
					return VariableType.Array_of_Floats;
				}

				return VariableType.Float;
			}
			if(variableName.Contains("$")) {
				if(isArray) {
					return VariableType.Array_of_Strings;
				}

				return VariableType.String;
			}
			if(isArray) {
				return VariableType.Array_of_Ints;
			}

			return VariableType.Int;
		}

		public class Construct {
			public string Name { get; set; }
			public ConstructType ConstructType { get; set; }
			public List<string> Parameters { get; set; }
		}
	}
}
