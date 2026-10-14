using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TemplateGuard.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TemplateGuardCodeFixProvider)), Shared]
    public class TemplateGuardCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("TG002", "TG003");

        public sealed override FixAllProvider GetFixAllProvider() =>
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null) return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            if (diagnostic.Id == "TG002")
            {
                var methodDecl = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                if (methodDecl != null)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Add [BaseModification] attribute template",
                            createChangedDocument: c => AddBaseModificationAttributeAsync(context.Document, root, methodDecl, c),
                            equivalenceKey: nameof(TemplateGuardCodeFixProvider) + "_TG002"),
                        diagnostic);
                }
            }
        }

        private Task<Document> AddBaseModificationAttributeAsync(Document document, SyntaxNode root, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            var dateStr = System.DateTime.UtcNow.ToString("yyyy-MM-dd");
            var authorStr = "Developer";

            var attribute = SyntaxFactory.Attribute(
                SyntaxFactory.IdentifierName("BaseModification"),
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SeparatedList(new[]
                    {
                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(authorStr))),
                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(dateStr))),
                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("TICKET-ID"))),
                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Explain why base method was modified"))),
                        SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Original behavior prior to modification")))
                    })));

            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            var newMethodDecl = methodDecl.WithAttributeLists(methodDecl.AttributeLists.Insert(0, attributeList));
            var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);

            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }
    }
}
