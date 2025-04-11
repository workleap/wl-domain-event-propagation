using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Workleap.DomainEventPropagation.Analyzers.Internals;

namespace Workleap.DomainEventPropagation.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventDomainAttributeUsageAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor UseDomainEventAttribute = new DiagnosticDescriptor(
        id: RuleIdentifiers.UseDomainEventAttribute,
        title: "Use DomainEvent attribute on event",
        messageFormat: "Use the DomainEvent attribute",
        category: RuleCategories.Usage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: RuleIdentifiers.HelpUri);

    internal static readonly DiagnosticDescriptor UseUniqueNameAttribute = new DiagnosticDescriptor(
        id: RuleIdentifiers.UseUniqueNameForAttributeValue,
        title: "Use unique event name in attribute",
        messageFormat: "Use unique event name in attribute",
        category: RuleCategories.Usage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: RuleIdentifiers.HelpUri);

    internal static readonly DiagnosticDescriptor FollowNamingConventionAttribute = new DiagnosticDescriptor(
        id: RuleIdentifiers.FollowNamingConventionAttributeValue,
        title: "Follow naming convention in DomainEvent attribute",
        messageFormat: "Follow naming convention in DomainEvent attribute: {0}",
        description: "Follow naming convention in attribute, the DomainEvent must be all in lowercase and follow the reverse dns format com.{Product}.{DomainService}.{Action} or com.{Product}.{DomainService}.{Entity}.{Action}",
        category: RuleCategories.Usage,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: RuleIdentifiers.HelpUri);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        UseDomainEventAttribute, UseUniqueNameAttribute, FollowNamingConventionAttribute);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStarted);
    }

    private static void OnCompilationStarted(CompilationStartAnalysisContext context)
    {
        var analyzer = new AnalyzerImplementation(context.Compilation);
        if (analyzer.IsValid)
        {
            context.RegisterSymbolAction(analyzer.AnalyzeOperationInvocation, SymbolKind.NamedType);
            context.RegisterCompilationEndAction(analyzer.CompilationEnd);
        }
    }

    private sealed class AnalyzerImplementation
    {
        private static readonly HashSet<string> AllowedProductNames = ["workleap", "officevibe", "lms", "skills", "onboarding", "sharegate"];
        private readonly INamedTypeSymbol? _domainEventInterfaceType;
        private readonly INamedTypeSymbol? _domainEventAttributeType;
        private readonly ConcurrentDictionary<string, List<INamedTypeSymbol>> _existingAttributes = new ConcurrentDictionary<string, List<INamedTypeSymbol>>(StringComparer.OrdinalIgnoreCase);

        public AnalyzerImplementation(Compilation compilation)
        {
            this._domainEventInterfaceType = compilation.GetBestTypeByMetadataName(KnownSymbolNames.DomainEventInterface, KnownSymbolNames.WorkleapDomainEventPropagationAbstractionsAssembly)!;
            this._domainEventAttributeType = compilation.GetBestTypeByMetadataName(KnownSymbolNames.DomainEventAttribute, KnownSymbolNames.WorkleapDomainEventPropagationAbstractionsAssembly)!;

            var domainEventPropagationSymbols = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            domainEventPropagationSymbols.AddIfNotNull(this._domainEventInterfaceType);
            domainEventPropagationSymbols.AddIfNotNull(this._domainEventAttributeType);
        }

        public bool IsValid => this._domainEventInterfaceType is not null &&
                               this._domainEventAttributeType is not null;

        public void AnalyzeOperationInvocation(SymbolAnalysisContext context)
        {
            if (context.Symbol is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct, IsAbstract: false } classTypeSymbol)
            {
                if (this.ImplementsBaseDomainEventInterface(classTypeSymbol))
                {
                    var domainEventAttribute = classTypeSymbol.GetAttributes()
                        .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, this._domainEventAttributeType));

                    if (domainEventAttribute is null)
                    {
                        context.ReportDiagnostic(UseDomainEventAttribute, classTypeSymbol);
                    }
                    else if (domainEventAttribute.ConstructorArguments.Length == 1)
                    {
                        var attributeArgument = domainEventAttribute.ConstructorArguments[0].Value;
                        if (attributeArgument is string attributeArgumentString)
                        {
                            var list = this._existingAttributes.GetOrAdd(attributeArgumentString, _ => new());
                            lock (list)
                            {
                                list.Add(classTypeSymbol);
                            }

                            ValidateEventNameConvention(context, attributeArgumentString, domainEventAttribute, classTypeSymbol);
                        }
                    }
                }
            }
        }

        private static void ValidateEventNameConvention(SymbolAnalysisContext context, string attributeArgumentString, AttributeData domainEventAttribute, INamedTypeSymbol classTypeSymbol)
        {
            if (IsEventNameFollowingConvention(attributeArgumentString, out var invalidNameReason))
            {
                return;
            }

            // Report on the string literal of the DomainEvent attribute name instead of class name.
            var domainEventSyntax = domainEventAttribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken);
            if (domainEventSyntax is AttributeSyntax attributeSyntax)
            {
                if (attributeSyntax.ArgumentList is { Arguments.Count: > 0 })
                {
                    var attributeArgumentSyntax = attributeSyntax.ArgumentList.Arguments[0];
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            FollowNamingConventionAttribute,
                            attributeArgumentSyntax.Expression.GetLocation(),
                            invalidNameReason));
                    return;
                }
            }

            context.ReportDiagnostic(FollowNamingConventionAttribute, classTypeSymbol);
        }

        private static bool IsEventNameFollowingConvention(string eventName, out string invalidNameReason)
        {
            if (!eventName.StartsWith("com.", StringComparison.Ordinal))
            {
                invalidNameReason = "The domain event name should start with com.";
                return false;
            }

            var listOfUrlComponents = eventName.Split('.');
            if (listOfUrlComponents.Length < 4)
            {
                invalidNameReason = "The domain event name format should follow the formats: com.{Product}.{DomainService}.{Action} or com.{Product}.{DomainService}.{Entity}.{Action}";
                return false;
            }

            if (listOfUrlComponents.Any(s => s.Length == 0))
            {
                invalidNameReason = "The domain event name should not have empty segments.";
                return false;
            }

            if (!AllowedProductNames.Contains(listOfUrlComponents[1]))
            {
                invalidNameReason = $"The domain event name product field should be part of the product list: [{string.Join(", ", AllowedProductNames)}]";
                return false;
            }

            if (!eventName.All(c => c is >= 'a' and <= 'z' or '.'))
            {
                invalidNameReason = "The domain event name should only consist of lowercase alphabetic characters and periods.";
                return false;
            }

            invalidNameReason = string.Empty;
            return true;
        }

        private bool ImplementsBaseDomainEventInterface(ITypeSymbol type)
        {
            return type.AllInterfaces.Any(x => SymbolEqualityComparer.Default.Equals(x, this._domainEventInterfaceType));
        }

        internal void CompilationEnd(CompilationAnalysisContext context)
        {
            foreach (var kvp in this._existingAttributes)
            {
                if (kvp.Value.Count > 1)
                {
                    foreach (var symbol in kvp.Value)
                    {
                        if (symbol.DeclaringSyntaxReferences.Length == 0)
                        {
                            continue;
                        }

                        var location = symbol.Locations[0];
                        var additionalLocations = symbol.Locations.Skip(1);
                        context.ReportDiagnostic(Diagnostic.Create(UseUniqueNameAttribute, location, additionalLocations));
                    }
                }
            }
        }
    }
}