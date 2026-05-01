namespace ClassSaver;

/// <summary>
/// Ignores the variable for serializing.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public class DoNotSerialize : Attribute
{
    
}