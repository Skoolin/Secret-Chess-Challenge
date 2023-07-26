using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ChessChallenge.Application
{
    public static class TokenCounter
    {

        static readonly HashSet<SyntaxKind> tokensToIgnore = new(new SyntaxKind[]
        {
            SyntaxKind.PrivateKeyword,
            SyntaxKind.PublicKeyword,
            SyntaxKind.SemicolonToken,
            SyntaxKind.CommaToken,
            SyntaxKind.ReadOnlyKeyword,
            // only count open brace since I want to count the pair as a single token
            SyntaxKind.CloseBraceToken,
            SyntaxKind.CloseBracketToken,
            SyntaxKind.CloseParenToken
        });

        public static int CountTokens(string code)
        {
            var builder = new StringBuilder();
            var lines = code.Split(Environment.NewLine, StringSplitOptions.None);

            // Use a regular expression to filter out lines containing "#DEBUG"
            var pattern = @"#DEBUG\b";
            foreach (var line in lines)
            {
                if (!Regex.IsMatch(line, pattern))
                    builder.AppendLine(line);
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(builder.ToString());
            SyntaxNode root = tree.GetRoot();
            return CountTokens(root);
        }

        static int CountTokens(SyntaxNodeOrToken syntaxNode)
        {
            SyntaxKind kind = syntaxNode.Kind();
            int numTokensInChildren = 0;


            foreach (var child in syntaxNode.ChildNodesAndTokens())
            {
                numTokensInChildren += CountTokens(child);
            }

            if (syntaxNode.IsToken && !tokensToIgnore.Contains(kind))
            {
                //Console.WriteLine(kind + "  " + syntaxNode.ToString());

                // String literals count for as many chars as are in the string
                if (kind is SyntaxKind.StringLiteralToken or SyntaxKind.InterpolatedStringTextToken)
                {
                    return syntaxNode.ToString().Length;
                }

                // Regular tokens count as just one token
                return 1;
            }

            return numTokensInChildren;
        }

    }
}