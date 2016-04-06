using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using static Dark_Basic_App_Parser.Helper;
using static Dark_Basic_App_Parser.VariableParser;

namespace Dark_Basic_App_Parser {

	public class App {
		private readonly string outputFile = Environment.CurrentDirectory + @"\output.html";
		private readonly string badListFile = Environment.CurrentDirectory + @"\badList.txt";
		private VariableParser parser;
		public void Run(string[] args) {
#if DEBUG
			args = new[] { Environment.CurrentDirectory + @"\game - Copy.dba" };
#endif
			if(!File.Exists(badListFile)) {
				Console.WriteLine(@"Missing ""bad list""");
				Console.ReadLine();
				Environment.Exit(0);
			}

			var badList = File.ReadAllLines(badListFile).ToList();
			for(int i = 0; i < badList.Count; i++) {
				badList[i] = badList[i].ToLower();
			}

			parser = new VariableParser {
				badList = badList
            };

			if(args.Length == 1) {
				var outputText = new StringBuilder();
				outputText.Append(Properties.Resources.Bootstrap_Config_html);
				foreach(var arg in args) {
					Console.WriteLine("Argument: " + arg);
				}

				//var nonVariableWords = new List<string> { "if", "`", "for", "while", "repeat", "until" };

				var removableVariableWords = new List<string> { "if", "while", "until" };
				var constructDeclarationWords = new List<string> { "function", "endfunction", ":", "return" };

				var variables = new List<Variable>();
				var subroutinesAndFunctions = new List<FunctionOrSubroutine>();
				if(File.Exists(args[0])) {
					Console.WriteLine("Finding functions/subroutines");

					//Add subroutines/functions first
					using(var sr = new StreamReader(args[0])) {
						string lineContent;
						var curLine = 1;
						while(sr.Peek() >= 0) {
							lineContent = sr.ReadLine()?.Trim();
							if(lineContent != null) {
								//Add subroutine/function
								if(constructDeclarationWords.Any(f => lineContent.ToLower().Contains(f)) && !lineContent.Contains("`")) {
									if(lineContent.Contains(":") && !lineContent.Contains(" ")) {
										//Subroutine start
										var subroutineName = lineContent.Substring(0, lineContent.IndexOf(":", StringComparison.Ordinal));
										subroutinesAndFunctions.Add(new FunctionOrSubroutine {
											Name = subroutineName,
											TypeOfConstruct = ConstructType.Subroutine,
											LineDeclaredAt = new List<int> { curLine },
											LinesUsedOn = new List<int>()
										});
									} else if(lineContent.StartsWith("return")) {
										//End subroutine
										subroutinesAndFunctions[subroutinesAndFunctions.Count - 1].LineDeclaredAt.Add(curLine);
									} else if(lineContent.ToLower().StartsWith("function ") && !lineContent.ToLower().Contains("endfunction")) {
										//Function start
										var functionName = GetStringBetween(lineContent, "function ", "(").Replace("unction ", "");
										var parameters = GetStringBetween(lineContent, "(", ")").Split(',');
										subroutinesAndFunctions.Add(new FunctionOrSubroutine {
											Name = functionName,
											TypeOfConstruct = ConstructType.Function,
											Parameters = parameters.ToList(),
											LineDeclaredAt = new List<int> { curLine },
											LinesUsedOn = new List<int>()
										});
									} else if(lineContent.ToLower().StartsWith("endfunction")) {
										//End function
										subroutinesAndFunctions[subroutinesAndFunctions.Count - 1].LineDeclaredAt.Add(curLine);
										subroutinesAndFunctions[subroutinesAndFunctions.Count - 1].ReturnValue = lineContent.Replace("endfunction", "");
									}
								}
							}

							curLine++;
						}

						Console.WriteLine("Found " + subroutinesAndFunctions.Count + " subroutines/functions");
						Console.WriteLine("Finding variables");

						//Add variables and calls to methods
						using(var srTwo = new StreamReader(args[0])) {
							curLine = 1;
							//var lines = srTwo.ReadToEnd().Split(new[] { "\r" }, StringSplitOptions.RemoveEmptyEntries);
							while(!srTwo.EndOfStream) {
								lineContent = srTwo.ReadLine();
								if(lineContent != null) {
									//Check if we're dealing with a variable
									if(!string.IsNullOrEmpty(lineContent)) {
										if(!parser.VariableContainsInvalidCharacters(lineContent)) {
											//bool dealtWith = false;
											//foreach(var word in lineContent.Split(' ')) {
											//	var index = variables.FindIndex(f => f.Name == word);
											//	if(index != -1) {
											//		//Already in list, so we're using it's value (in DB)
											//		variables[index].LinesUsedOn.Add(curLine);
											//		dealtWith = true;
											//		break;
											//	}
											//}

											//if(!dealtWith) {
											////var potentialVariableList = lineContent.Split(new[] { " and ", " to ", " or ", "+", "-", "<", "<=", ">", ">=", "," }, StringSplitOptions.RemoveEmptyEntries);
											//if(potentialVariableList.Length >= 1) {
											//foreach(var itm in potentialVariableList) {
											DealWithVariableAndSubroutineAppropriately(lineContent, curLine, removableVariableWords, ref variables, subroutinesAndFunctions, constructDeclarationWords);
											//	}
											//}
											//}
										}
									}
								}

								curLine++;
							}
						}

						Console.WriteLine("Found " + variables.Count + " variables");
						Console.WriteLine("Generating tables...");

						var whereUsed = new StringBuilder();
						var firstTable = new StringBuilder();
						firstTable = GenerateFirstTable(firstTable, whereUsed, variables, subroutinesAndFunctions);
						Console.WriteLine("Generated first table.");

						var secondTable = new StringBuilder();
						secondTable = GenerateSecondTable(secondTable, subroutinesAndFunctions);

						Console.WriteLine("Generated second table.");

						outputText.Replace("{FIRST_TABLE_HERE}", firstTable.ToString());
						outputText.Replace("{SECOND_TABLE_HERE}", secondTable.ToString());

						File.WriteAllText(outputFile, outputText.ToString());

						Console.WriteLine("File has been generated.");
					}
				} else {
					Console.WriteLine("Input file doesn't exist");
				}
			} else {
				Console.WriteLine("Input file argument is missing.");
			}

			Console.ReadLine();
		}

		private StringBuilder GenerateFirstTable(StringBuilder firstTable, StringBuilder whereUsed, List<Variable> variables, List<FunctionOrSubroutine> subroutinesAndFunctions) {
			var whereUsedFuncList = new List<string>();

			foreach(var variable in variables) {
				firstTable.AppendLine("<tr>");
				firstTable.AppendLine("<td>" + variable.Name + "</td>");                                                    //Var name
				firstTable.AppendLine("<td>" + variable.TypeOfData.ToString().Replace("_", " ") + "</td>");                 //Data type
				firstTable.AppendLine("<td>" + variable.Scope + "</td>");                                                   //Scope
				firstTable.AppendLine("<td></td>");                                                                         //Description
				firstTable.AppendLine("<td>" + "Line " + variable.LineDeclaredAt + "</td>");                                //Declaration

				foreach(var construct in subroutinesAndFunctions) {
					//Where used

					//Go through every construct
					//Check if potentialVariable is being used inside of that construct
					//if(construct.LinesUsedOn.Count == 0) {
					//whereUsed.Append("Not used");
					//} else {
					//Check if potentialVariable is being used inside of method
					foreach(var variableUsedLine in variable.LinesUsedOn) {
						if(variableUsedLine >= construct.LineDeclaredAt[0] && variableUsedLine <= construct.LineDeclaredAt[1]) {
							if(!whereUsedFuncList.Contains(construct.Name)) {
								whereUsed.Append(construct.Name + ", ");
								whereUsedFuncList.Add(construct.Name);
							}
						}
					}
					//}
				}

				firstTable.AppendLine("<td>" + whereUsed + "</td>");
				firstTable.AppendLine("</tr>");
				whereUsedFuncList.Clear();
				whereUsed.Clear();
			}

			return firstTable;
		}

		private StringBuilder GenerateSecondTable(StringBuilder secondTable, List<FunctionOrSubroutine> subroutinesAndFunctions) {
			foreach(var construct in subroutinesAndFunctions) {
				secondTable.AppendLine("<tr>");
				//Name
				secondTable.AppendLine("<td>" + construct.Name + "</td>");
				//Description
				secondTable.AppendLine("<td>" + construct.TypeOfConstruct + " - </td>");

				//Parameters
				if(construct.Parameters?.Count >= 1) {
					secondTable.AppendLine("<td>" + string.Join(", ", construct.Parameters) + "</td>");
				} else {
					secondTable.AppendLine("<td>None</td>");
				}

				//Return value
				if(construct.TypeOfConstruct == ConstructType.Function) {
					secondTable.AppendLine("<td>" + construct.ReturnValue + "</td>");
				} else {
					secondTable.AppendLine("<td></td>");
				}
				secondTable.AppendLine("</tr>");
			}
			return secondTable;
		}

		//Add variable to the variables list, after it has been checked that it is actually a variable
		private static void AddVariable(List<FunctionOrSubroutine> subroutinesAndFunctions, ref List<Variable> variables, Variable variable, int curLine) {
			foreach(var construct in subroutinesAndFunctions) {
				//Go through every construct
				//Check if potential variable is being used inside of that construct
				if(construct.LineDeclaredAt.Count > 0) {
					//We're inside of a function and have been declared, we are in local scope
					if(curLine > construct.LineDeclaredAt[0] && curLine < construct.LineDeclaredAt[1]) {
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

		private void DealWithVariableAndSubroutineAppropriately(string lineContent, int curLine, List<string> removableVariableWords, ref List<Variable> variables, List<FunctionOrSubroutine> subroutinesAndFunctions, List<string> constructDeclarationWords) {
			//if(lineContent.Contains("(") && lineContent.Contains(")") && lineContent.Contains(",")) {
			//We received an if statement, or an array which uses a variable inside of it
			//List<string> possibleVars = GetStringBetween(lineContent, "(", ")").Split(',').ToList();

			_DealWithVariableAndSubroutineAppropriately(lineContent, curLine, removableVariableWords, ref variables, subroutinesAndFunctions, constructDeclarationWords);

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

		//Can I get a local function yet??? This will only be used inside DealWithVariableAndSubroutineAppropriately anyway
		private void _DealWithVariableAndSubroutineAppropriately(string lineContent, int curLine, List<string> removableVariableWords, ref List<Variable> variables, List<FunctionOrSubroutine> subroutinesAndFunctions, List<string> constructDeclarationWords) {
			var varList = parser.ValidateAndParseVariable(lineContent, curLine, removableVariableWords, subroutinesAndFunctions);
			foreach(var variable in varList) {
				if(variable.Name != null) {
					//Dealing with a variable of some kind
					AddVariable(subroutinesAndFunctions, ref variables, variable, curLine);
				}
			}
		}
	}
}