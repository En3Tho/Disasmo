namespace Disasmo.Runner;

public class SymbolInfo
{
    public string QualifiedClassName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public string? MethodName { get; set; }
    public bool IsLocalFunction { get; set; }
}