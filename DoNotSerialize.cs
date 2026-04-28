namespace ClassSaver;

/// <summary>
/// Ignores the variable for serializing.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class DoNotSerialize : Attribute
{
    
}