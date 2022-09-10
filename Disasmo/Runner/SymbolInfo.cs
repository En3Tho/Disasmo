namespace Disasmo.Runner;

public class SymbolInfo
{
    public string QualifiedClassName { get; set; }
    public string ClassName { get; set; }
    public string? MethodName { get; set; }
    public bool IsLocalFunction { get; set; }
}