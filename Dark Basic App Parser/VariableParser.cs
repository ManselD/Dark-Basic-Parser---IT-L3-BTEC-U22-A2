using System;
using System.Collections.Generic;
using System.Linq;
using static Dark_Basic_App_Parser.Helper;

namespace Dark_Basic_App_Parser {

	public class VariableParser {
		public List<string> badList;

		public bool VariableContainsInvalidCharacters(string potentialVariable) {
			var badCharacters = new List<string> { "`" };
			if(badCharacters.Any(f => potentialVariable.StartsWith(f))) {
				return true;
			}

			return false;
		}

		public string CheckForBadWords(string[] badWords, string[] potentialBadWords, string line) {
			line = line.Trim();

			//Full match
			if(badWords.ToString() == line) {
				line = line.Replace(badWords.ToString(), "");
			} else {
				//string leftoverStr = null;
				string replacementStr = null;
				//Look for another match by seperating everything in words (spaces)
				for(int i = 0; i < potentialBadWords.Length; i++) {
					//if(i + 1 == potentialBadWords.Length) {
					//	if(potentialBadWords[i] == badWords.ToString()) {
					//		replacementStr += potentialBadWords[i];
					//		break;
					//	}
					//} else {
							foreach(var word in badWords) {
								if(potentialBadWords[i] == word) {
							//replacementStr += potentialBadWords[i] + " ";
									line = line.Replace(word, "");
								}
							}

					//}
				}

				//var previousVal = new StringBuilder();
				//var potPreviousVal = new StringBuilder();
				//var matchingStrs = new List<string>();
				//for(int pv = 0; pv < badWords.Length; pv++) {
				//	previousVal.Append(badWords[pv] + " ");
				//	for(int ppv = 0; ppv < potentialBadWords.Length; ppv++) {
				//		potPreviousVal.Append(potentialBadWords[ppv] + " ");
				//		//We found a match!
				//		if(previousVal.ToString().Trim() == potPreviousVal.ToString().Trim()) {
				//			matchingStrs.Add(previousVal.ToString().Trim());
				//                        break;
				//		} else {
				//		}
				//	}
				//	potPreviousVal.Clear();
				//	//if(!string.IsNullOrEmpty(matchingStr)) {
				//	//	line = line.Replace(matchingStr, "");
				//	//	matchingStr = null;
				//	//}
				//}

				//foreach(var matchingVal in matchingStrs) {
				//	line = line.Replace(matchingVal, "");
				//}

				//Check if the current word + another one in the potential bad word array is equal to the bad word.
				//if(potentialBadWords.Length >= curPos + 2) {
				//	if(potentialBadWords[curPos] + " " + potentialBadWords[curPos + 1] == badWords[curPos]) {
				//		line = line.Replace(badWords[curPos] + badWords[curPos + 1], "");

				//		if(potentialBadWords.Length >= curPos + 2) {
				//			line = CheckForBadWords(badWords, potentialBadWords, line, curPos + 1);
				//		}
				//	}

				//	line = line.Replace(badWords[curPos], "");
				//}
			}

			return line;
		}

		public string RemoveBadWords(string line) {
			//Remove any non-useful function calls (those built into dark basic)
			var potentialBadWords = line.Split(new [] { " ", "(", ")", "=", "*", "%", "/", "+", "-" }, StringSplitOptions.RemoveEmptyEntries);
			foreach(var badString in badList) {
				var badWords = badString.Split(' ');
				if(line.Contains(badString)) {
					line = CheckForBadWords(badWords, potentialBadWords, line);
				}
			}

			return line;
		}

		public bool IsVariable(string var, List<FunctionOrSubroutine> subroutinesAndFunctionsList) {
			return var.Any(char.IsLetter) && !var.Contains("\"") && subroutinesAndFunctionsList.All(f => f.Name != var);
		}

		public List<string> ParseFunctionCall(string call, int curLine, out string returnVal) {
			if(call.Contains("(") && call.Contains(")")) { //&& !call.StartsWith("dim")
				int amountOfFunctionCalls = 0;
				int currentFuncCount = 0;
				foreach(var character in call){
					if(character == '(') {
						currentFuncCount += 1;
                    }else if(character == ')') {
						currentFuncCount -= 1;
						if(currentFuncCount == 0) {
							amountOfFunctionCalls += 1;
						} else {
							Console.WriteLine("There is a syntax error in the program. Line: " + curLine);
						}
					}
				}

				string returnValue = null;
				var vars = new List<string>();
				for(int i = 0; i < amountOfFunctionCalls; i++) {
					var parameters = GetStringBetween(call, "(", ")"); //call.Substring(call.IndexOf("(", StringComparison.Ordinal));
					if(!string.IsNullOrEmpty(parameters)) {
						vars.AddRange(parameters.Split(new[] { "(", ",", ")" }, StringSplitOptions.RemoveEmptyEntries));
						returnValue += call.Substring(0, call.IndexOf("(", StringComparison.Ordinal));
					}
				}

				if(returnValue != null && returnValue.StartsWith("dim")) {
					returnValue = returnValue.Remove(0, 3);
                }

				returnVal = returnValue;
                return vars.ToList();
			} else {
				returnVal = null;
				return new List<string>();
			}
		}

		public List<Variable> ValidateAndParseVariable(string currentLine, int curLine, List<string> removableVariableWords, List<FunctionOrSubroutine> subroutinesAndFunctionsList) {
			currentLine = currentLine.Trim();
			var nonVariableWords = new List<string> { "repeat" };
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

			tmpVar = RemoveBadWords(currentLine);
			//Console.WriteLine("Removed bad words for line " + curLine);

			//Declaration of a variable
			var words = tmpVar.Split(new[] { " ", ",", ")", "(", "+", "-", "*", "/", "%" }, StringSplitOptions.RemoveEmptyEntries);
			if(words.Length >= 3) {
				//Check if second word used is an equals sign, represents the declaration of a new variable
				if(words[1] == "=") {
					varList.Add(new Variable {
						Name = words[0],
						LineDeclaredAt = curLine,
						TypeOfData = GetTypeOfData(words[0]),
						LinesUsedOn = new List<int>(),
						Scope = Scope.Global
					});
					Console.WriteLine("Processed line " + curLine);
					return varList;
				}
			}

			if(currentLine.StartsWith("dim")) {
				//Array declaration
				string arrName;
				var vars = ParseFunctionCall(tmpVar, curLine, out arrName);
				//var arrName = GetStringBetween(tmpVar, "dim ", "(");
                varList.Add(new Variable {
					Name = arrName,
					LineDeclaredAt = curLine,
					TypeOfData = GetTypeOfData(arrName, true),
					LinesUsedOn = new List<int>(),
					Scope = Scope.Global
				}); 

				foreach(var variable in vars) {
					if(IsVariable(variable, subroutinesAndFunctionsList)) {
						varList.Add(new Variable {
							Name = variable,
							LineDeclaredAt = curLine,
							TypeOfData = GetTypeOfData(variable),
							LinesUsedOn = new List<int>(),
							Scope = Scope.Global
						});
					}
				}

				Console.WriteLine("Processed line " + curLine);
				return varList;
			}

			//Check for variables being used
			if(currentLine.StartsWith("if")) {
				var lst = new List<string>();

				//Check inside of any function calls for parameters being sent over
				string newVersionOfLine;
				var funcParameters = ParseFunctionCall(tmpVar, curLine, out newVersionOfLine);
                lst.AddRange(funcParameters);

				if(newVersionOfLine != null) {
					lst.AddRange(newVersionOfLine.Split(new[] { " ", "and", "+", "-", "*", "/", "%", "=", ">", ">=", "<=", "<" }, StringSplitOptions.RemoveEmptyEntries));
				}

				foreach(var itm in lst) {
					if(IsVariable(itm, subroutinesAndFunctionsList)) {
						varList.Add(new Variable {
							Name = itm,
							LineDeclaredAt = curLine,
							TypeOfData = GetTypeOfData(itm),
							LinesUsedOn = new List<int>(),
							Scope = Scope.Global
						});
					}
					return varList;
				}
			} else {
				foreach(var itm in words) {
					if(IsVariable(itm, subroutinesAndFunctionsList)) {
						varList.Add(new Variable {
							Name = itm,
							LineDeclaredAt = curLine,
							TypeOfData = GetTypeOfData(itm),
							LinesUsedOn = new List<int>(),
							Scope = Scope.Global
						});
					}
					return varList;
				}
			}

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

			return varList;
		}
	}
}