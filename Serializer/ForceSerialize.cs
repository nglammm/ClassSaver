namespace ClassSaver;

/// <summary>
/// Used to force serialization on a variable (mostly on a private variable) to serialize.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ForceSerialize : Attribute
{
}