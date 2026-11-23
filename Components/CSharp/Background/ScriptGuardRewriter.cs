using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace StreamAssist.Components.CSharp.Background;



// adds the run's checks to a script before it's compiled (StreamAssist.Scripting.Runtime.ScriptGuard): Loop() at the
// top of every loop body (and every labelled statement, for goto loops), Call() at the start of every method, local
// function, accessor and block lambda. Nothing is put on a new line, so the lines a compile error or a stack trace
// gives are still the file's. Expression bodies become block bodies so they get a Call() too ("=> x" becomes
// "{ Call(); return x; }")
public sealed class ScriptGuardRewriter : CSharpSyntaxRewriter
{
    private const string Guard = "global::StreamAssist.Scripting.Runtime.ScriptGuard";

    private static StatementSyntax LoopCheck => ParseStatement($"{Guard}.Loop();");
    private static StatementSyntax CallCheck => ParseStatement($"{Guard}.Call();");



    public static SyntaxTree Rewrite(SyntaxTree tree) =>
        tree.WithRootAndOptions(new ScriptGuardRewriter().Visit(tree.GetRoot()), tree.Options);



    // loops



    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return visited.WithStatement(Checked(visited.Statement, LoopCheck));
    }



    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var visited = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return visited.WithStatement(Checked(visited.Statement, LoopCheck));
    }



    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
        return visited.WithStatement(Checked(visited.Statement, LoopCheck));
    }



    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return visited.WithStatement(Checked(visited.Statement, LoopCheck));
    }



    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        var visited = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return visited.WithStatement(Checked(visited.Statement, LoopCheck));
    }



    // a goto can loop back to a label (a declaration after one stays as it is: wrapping it would hide it)
    public override SyntaxNode? VisitLabeledStatement(LabeledStatementSyntax node)
    {
        var visited = (LabeledStatementSyntax)base.VisitLabeledStatement(node)!;
        if (visited.Statement is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax) return visited;
        return visited.WithStatement(Checked(visited.Statement, LoopCheck));
    }



    // methods



    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var visited = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (visited.Body != null) return visited.WithBody(Checked(visited.Body));
        if (visited.ExpressionBody == null) return visited;

        var block = FromExpression(visited.ExpressionBody, visited.SemicolonToken, Returns(visited.ReturnType, visited.Modifiers));
        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithBody(block);
    }



    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var visited = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
        if (visited.Body != null) return visited.WithBody(Checked(visited.Body));
        if (visited.ExpressionBody == null) return visited;

        var block = FromExpression(visited.ExpressionBody, visited.SemicolonToken, Returns(visited.ReturnType, visited.Modifiers));
        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithBody(block);
    }



    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var visited = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (visited.Body != null) return visited.WithBody(Checked(visited.Body));
        if (visited.ExpressionBody == null) return visited;

        var block = FromExpression(visited.ExpressionBody, visited.SemicolonToken, returns: false);
        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithBody(block);
    }



    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
    {
        var visited = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;
        if (visited.Body != null) return visited.WithBody(Checked(visited.Body));
        if (visited.ExpressionBody == null) return visited;

        var block = FromExpression(visited.ExpressionBody, visited.SemicolonToken, returns: true);
        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithBody(block);
    }



    public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
    {
        var visited = (ConversionOperatorDeclarationSyntax)base.VisitConversionOperatorDeclaration(node)!;
        if (visited.Body != null) return visited.WithBody(Checked(visited.Body));
        if (visited.ExpressionBody == null) return visited;

        var block = FromExpression(visited.ExpressionBody, visited.SemicolonToken, returns: true);
        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithBody(block);
    }



    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        var visited = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
        if (visited.Body != null) return visited.WithBody(Checked(visited.Body));
        if (visited.ExpressionBody == null) return visited;

        var returns = visited.Keyword.IsKind(SyntaxKind.GetKeyword);
        var block = FromExpression(visited.ExpressionBody, visited.SemicolonToken, returns);
        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithBody(block);
    }



    // "int X => x;" becomes "int X { get { Call(); return x; } }"
    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var visited = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
        if (visited.ExpressionBody == null) return visited;

        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithAccessorList(Getter(visited.ExpressionBody, visited.SemicolonToken));
    }



    public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        var visited = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!;
        if (visited.ExpressionBody == null) return visited;

        return visited.WithExpressionBody(null).WithSemicolonToken(default).WithAccessorList(Getter(visited.ExpressionBody, visited.SemicolonToken));
    }



    // lambdas with a block body (an expression lambda's type isn't known here, so it stays as it is)



    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        var visited = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node)!;
        return visited.Block == null ? visited : visited.WithBlock(Checked(visited.Block));
    }



    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        var visited = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;
        return visited.Block == null ? visited : visited.WithBlock(Checked(visited.Block));
    }



    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        var visited = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node)!;
        return visited.WithBlock(Checked(visited.Block));
    }



    // helpers



    // a block with the check first, on the line its opening brace ends (nothing moves to another line)
    private static BlockSyntax Checked(BlockSyntax block) => block.WithStatements(block.Statements.Insert(0, CallCheck));



    // a loop's body with the check first; a single statement becomes a block around it, with no line breaks added
    private static StatementSyntax Checked(StatementSyntax body, StatementSyntax check) => body is BlockSyntax block
        ? block.WithStatements(block.Statements.Insert(0, check))
        : Block(check, body);



    // "=> expression;" as "{ Call(); return expression; }" (or without the return), using the arrow's and the
    // semicolon's spacing for the braces so the lines stay where they were
    private static BlockSyntax FromExpression(ArrowExpressionClauseSyntax arrow, SyntaxToken semicolon, bool returns)
    {
        var expression = arrow.Expression;
        StatementSyntax statement = expression switch
        {
            ThrowExpressionSyntax thrown => ThrowStatement(thrown.ThrowKeyword, thrown.Expression, Token(SyntaxKind.SemicolonToken)),
            _ when returns => ReturnStatement(Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(Space), expression, Token(SyntaxKind.SemicolonToken)),
            _ => ExpressionStatement(expression, Token(SyntaxKind.SemicolonToken)),
        };

        return Block(
            Token(arrow.ArrowToken.LeadingTrivia, SyntaxKind.OpenBraceToken, arrow.ArrowToken.TrailingTrivia),
            List(new[] { CallCheck, statement }),
            Token(semicolon.LeadingTrivia, SyntaxKind.CloseBraceToken, semicolon.TrailingTrivia));
    }



    private static AccessorListSyntax Getter(ArrowExpressionClauseSyntax arrow, SyntaxToken semicolon)
    {
        var body = FromExpression(arrow, semicolon, returns: true);
        var closing = body.CloseBraceToken;
        body = body.WithCloseBraceToken(closing.WithTrailingTrivia());

        return AccessorList(
            Token(SyntaxKind.OpenBraceToken),
            SingletonList(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, body).WithKeyword(Token(SyntaxKind.GetKeyword))),
            Token(default, SyntaxKind.CloseBraceToken, closing.TrailingTrivia));
    }



    // whether an expression body gives back a value: not for void, nor for an async method giving a plain Task
    private static bool Returns(TypeSyntax returnType, SyntaxTokenList modifiers)
    {
        if (returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }) return false;
        if (!modifiers.Any(SyntaxKind.AsyncKeyword)) return true;

        var name = returnType switch
        {
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax aliased => aliased.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };
        return !(name is IdentifierNameSyntax { Identifier.Text: "Task" or "ValueTask" });
    }
}
