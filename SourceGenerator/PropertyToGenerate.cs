namespace SourceGenerator;

public class PropertyToGenerate
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool IsNullable { get; set; }
}

public class ClassProperties
{
    public string ClassName { get; set; }
    public string FullClassName { get; set; }
    public PropertyToGenerate[] Properties { get; set; }
}