//
// VariableDeclaredWideScopeIssue.cs
//
// Author:
//       Simon Lindgren <simon.n.lindgren@gmail.com>
//
// Copyright (c) 2012 Simon Lindgren
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System.Collections.Generic;
using System.Linq;
using System;

namespace ICSharpCode.NRefactory.CSharp.Refactoring
{
	[IssueDescription("The variable can be declared in a nested scope",
	                   Description = "Highlights variables that can be declared in a nested scope.",
	                   Category = IssueCategories.Opportunities,
	                   Severity = Severity.Suggestion)]
	public class VariableDeclaredInWideScopeIssue : ICodeIssueProvider
	{
		#region ICodeIssueProvider implementation
		public IEnumerable<CodeIssue> GetIssues(BaseRefactoringContext context)
		{
			return new GatherVisitor(context, this).GetIssues();
		}
		#endregion

		class GatherVisitor : GatherVisitorBase
		{
			readonly BaseRefactoringContext context;
			
			public GatherVisitor(BaseRefactoringContext context, VariableDeclaredInWideScopeIssue inspector) : base (context)
			{
				this.context = context;
			}

			static IList<Type> moveTargetBlacklist = new List<Type>() {
				typeof(WhileStatement),
				typeof(ForeachStatement),
				typeof(ForStatement),
				typeof(DoWhileStatement),
				typeof(TryCatchStatement),
				typeof(AnonymousMethodExpression),
				typeof(LambdaExpression)
			};
		
			public override void VisitVariableDeclarationStatement(VariableDeclarationStatement variableDeclarationStatement)
			{
				base.VisitVariableDeclarationStatement(variableDeclarationStatement);

				if (!(variableDeclarationStatement.Parent is BlockStatement))
					// We are somewhere weird, like a the ResourceAquisition of a using statement
					return;

				if (variableDeclarationStatement.Variables.Count > 1)
					return;

				// Start at the parent node. Presumably this is a BlockStatement
				var rootNode = variableDeclarationStatement.Parent;
				var variableInitializer = variableDeclarationStatement.Variables.First();
				var identifiers = from node in rootNode.Descendants
					let identifier = node as IdentifierExpression
					where identifier != null && identifier.Identifier == variableInitializer.Name
						select node;

				if (identifiers.Count() == 0)
					// variable is not used
					return;

				AstNode lowestCommonAncestor = GetLowestCommonAncestor(rootNode, identifiers);
				var path = GetPath(rootNode, lowestCommonAncestor);

				var firstLoopStatement = (from node in path
				                          where moveTargetBlacklist.Contains(node.GetType())
				                          select node).FirstOrDefault();
				IList<AstNode> possibleDestinationsPath;
				if (firstLoopStatement == null) {
					possibleDestinationsPath = path;
				} else {
					possibleDestinationsPath = GetPath(rootNode, firstLoopStatement);
				}
				var mostNestedEmbeddedStatement = (from node in possibleDestinationsPath
				                                   let statement = node as Statement
				                                   let blockStatement = statement as BlockStatement
				                                   where statement != null && IsNestedScope(statement)
				                                   select statement).LastOrDefault();

				if (mostNestedEmbeddedStatement != null && mostNestedEmbeddedStatement != rootNode) {
					AddIssue(variableDeclarationStatement, context.TranslateString("Variable could be moved to a nested scope"),
					         GetActions(variableDeclarationStatement, mostNestedEmbeddedStatement));
				}
			}

			bool IsNestedScope(AstNode node)
			{
				var blockStatement = node as BlockStatement;
				if (blockStatement != null)
					return true;

				var statement = node as Statement;
				if (statement == null)
					return false;

				var role = node.Role;
				if (role == Roles.EmbeddedStatement ||
					role == IfElseStatement.TrueRole ||
					role == IfElseStatement.FalseRole) {
					return true;
				}
				return false;
			}

			IEnumerable<CodeAction> GetActions(VariableDeclarationStatement declaration, Statement targetScope)
			{
				yield return new CodeAction(context.TranslateString("Move to nested scope"), script => {
					var blockStatement = targetScope as BlockStatement;
					if (blockStatement == null) {
						var newBlockStatement = new BlockStatement {
							Statements = {
								declaration.Clone(),
								targetScope.Clone()
							}
						};
						script.Replace(targetScope, newBlockStatement);
						script.FormatText(targetScope.Parent);
					} else {
						script.InsertBefore(blockStatement.Statements.First(), declaration.Clone());
					}
					script.Remove(declaration);
				});
			}

			AstNode GetLowestCommonAncestor(AstNode assumedRoot, IEnumerable<AstNode> leaves)
			{
				var previousPath = GetPath(assumedRoot, leaves.First());
				int lowestIndex = previousPath.Count - 1;
				foreach (var leaf in leaves.Skip(1)) {
					var currentPath = GetPath(assumedRoot, leaf);
					lowestIndex = GetLowestCommonAncestorIndex(previousPath, currentPath, lowestIndex);
					previousPath = currentPath;
				}
				return previousPath [lowestIndex];
			}
			
			int GetLowestCommonAncestorIndex(IList<AstNode> path1, IList<AstNode> path2, int maxIndex)
			{
				var max = Math.Min(Math.Min(path1.Count, path2.Count), maxIndex);
				for (int i = 0; i <= max; i++) {
					if (path1 [i] != path2 [i])
						return i - 1;
				}
				return max;
			}

			IList<AstNode> GetPath(AstNode from, AstNode to)
			{
				var reversePath = new List<AstNode>();
				do {
					reversePath.Add(to);
					to = to.Parent;
				} while (to != from.Parent);
				reversePath.Reverse();
				return reversePath;
			}
		}
	}
}

