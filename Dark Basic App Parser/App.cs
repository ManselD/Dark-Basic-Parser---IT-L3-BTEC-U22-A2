using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dark_Basic_App_Parser.Properties;
using static Dark_Basic_App_Parser.Helper;

namespace Dark_Basic_App_Parser {

	public class App {
		private readonly string outputFile = Environment.CurrentDirectory + @"\output.html";
		private readonly string badListFile = Environment.CurrentDirectory + @"\badList.txt";
		private DarkBasicParser darkBasicParser;

		public void AddLog(string str) {
			Console.WriteLine(str);
			File.AppendAllText(Environment.CurrentDirectory + "log.txt", str + "\r\n");
		}

		public void Run(string[] args) {
			args = new []{ Environment.CurrentDirectory + @"\game.dba" };
			if(!File.Exists(badListFile)) {
				Console.WriteLine(@"Missing ""bad list""");
				Console.ReadLine();
				Environment.Exit(0);
			}

			var badList = File.ReadAllLines(badListFile).ToList();
			for(int i = 0; i < badList.Count; i++) {
				//Can't edit an array's contents whilst traversing it using foreach, and I believe for loops are more efficient anyway
				badList[i] = badList[i].ToLower();
			}

			darkBasicParser = new DarkBasicParser(badList);

			if(args.Length == 1) {
				var outputText = new StringBuilder();
				outputText.Append(Resources.Bootstrap_Config_html);
				foreach(var arg in args) {
					Console.WriteLine("Argument: " + arg);
				}

				//var nonVariableWords = new List<string> { "if", "`", "for", "while", "repeat", "until" };

				var removableVariableWords = new List<string> { "if", "while", "until" };
				
				if(File.Exists(args[0])) {
					var startTime = DateTime.Now;
					AddLog("STARTED - " + startTime);
					AddLog("Finding functions/subroutines");

					var filesToTraverse = darkBasicParser.GetIncludedFiles(args[0]);
					filesToTraverse.Add(args[0]);

					//Add subroutines/functions first
					foreach(var file in filesToTraverse) {
						var localFile = darkBasicParser.ConvertToLocalPath(file);
						using(var sr = new StreamReader(file)) {
							string lineContent;
							var curLine = 1;
							while(sr.Peek() >= 0) {
								lineContent = sr.ReadLine()?.Trim();
								if(lineContent != null) {
									//Add subroutine/function
									darkBasicParser.ParseFunctionAndSubroutine(lineContent, curLine, localFile);
								}

								curLine++;
							}
						}
					}

					AddLog("Found " + darkBasicParser.subroutinesAndFunctions.Count + " subroutines/functions");
					AddLog("Finding variables");

					//Add variables and calls to methods
					foreach(var file in filesToTraverse) {
						var localFile = darkBasicParser.ConvertToLocalPath(file);
						using(var sr = new StreamReader(file)) {
							var lines = sr.ReadToEnd().Split(new[] { "\r\n" }, StringSplitOptions.None);
							//while(!srTwo.EndOfStream) {
							for(int i = 0; i < lines.Length; i++) {
								var lineContent = lines[i];
								//Check if we're dealing with a variable
								if(!string.IsNullOrEmpty(lineContent)) {
									if(!darkBasicParser.VariableContainsInvalidCharacters(lineContent)) {
										darkBasicParser.DealWithVariableAndSubroutineAppropriately(lineContent, i + 1, removableVariableWords, localFile);
										AddLog("Processed line " + (i + 1) + " in " + localFile);
									}
								}
							}
						}
					}

					AddLog("Found " + darkBasicParser.variables.Count + " variables");
					AddLog("Generating tables...");

					//Generate output
					var firstTable = new StringBuilder();
					firstTable = GenerateFirstTable(firstTable, darkBasicParser.variables, darkBasicParser.subroutinesAndFunctions);
					AddLog("Generated first table.");

					var secondTable = new StringBuilder();
					secondTable = GenerateSecondTable(secondTable, darkBasicParser.subroutinesAndFunctions);

					AddLog("Generated second table.");

					outputText.Replace("{FIRST_TABLE_HERE}", firstTable.ToString());
					outputText.Replace("{SECOND_TABLE_HERE}", secondTable.ToString());

					File.WriteAllText(outputFile, outputText.ToString());

					AddLog("File has been generated.");

					var finishTime = DateTime.Now;
					AddLog("COMPLETED - " + finishTime + " - Took " + (finishTime - startTime));
					AddLog(null);
				} else {
					AddLog("Input file doesn't exist");
				}
			} else {
				AddLog("Input file argument is missing.");
			}

			Console.ReadLine();
		}

		private StringBuilder GenerateFirstTable(StringBuilder firstTable, List<Variable> variables, List<FunctionOrSubroutine> subroutinesAndFunctions) {
			var whereUsedFuncList = new List<string>();

			foreach(var variable in variables) {
				firstTable.AppendLine("<tr>");
				firstTable.AppendLine("<td>" + variable.Name + "</td>");                                                    //Var name
				firstTable.AppendLine("<td>" + variable.TypeOfData.ToString().Replace("_", " ") + "</td>");                 //Data type
				firstTable.AppendLine("<td>" + variable.File.Remove(0, 1) + " - " + variable.Scope + "</td>");                                                   //Scope
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
						if(construct.LineDeclaredAt.Count == 2 && variableUsedLine >= construct.LineDeclaredAt[0] && variableUsedLine <= construct.LineDeclaredAt[1]) {
							if(!whereUsedFuncList.Contains(construct.Name)) {
								whereUsedFuncList.Add(construct.Name);
							}
						}
					}
					//}
				}

				if(whereUsedFuncList.Count == 0) {
					whereUsedFuncList.Add("Main loop");
				}

				firstTable.AppendLine("<td>" + string.Join(", ", whereUsedFuncList) + "</td>");
				firstTable.AppendLine("</tr>");
				whereUsedFuncList.Clear();
			}

			return firstTable;
		}

		private StringBuilder GenerateSecondTable(StringBuilder secondTable, List<FunctionOrSubroutine> subroutinesAndFunctions) {
			foreach(var construct in subroutinesAndFunctions) {
				secondTable.AppendLine("<tr>");
				//Name
				secondTable.AppendLine("<td>" + construct.Name + "</td>");
				//Description
				secondTable.AppendLine("<td>" + construct.File.Remove(0, 1) + " - " + construct.TypeOfConstruct + " - </td>");

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
	}
}