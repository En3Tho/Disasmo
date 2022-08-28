using Microsoft.CodeAnalysis;
using SymbolInfo = Disasmo.Runner.SymbolInfo;

namespace Disasmo;

public static class SymbolExtensions
{
    public static SymbolInfo ToSymbolInfo(this ISymbol symbol)
    {
        if (symbol is IMethodSymbol methodSymbol)
        {
            return new ()
            {
                ClassName = symbol.ContainingSymbol.Name,
                IsLocalFunction = methodSymbol.Kind == SymbolKind.Local,
                MethodName = symbol.Name,
                QualifiedClassName = symbol.ContainingSymbol.ToString()
            };
        }

        return new()
        {
            ClassName = symbol.Name,
            IsLocalFunction = false,
            MethodName = null,
            QualifiedClassName = symbol.ToString()
        };
    }
}