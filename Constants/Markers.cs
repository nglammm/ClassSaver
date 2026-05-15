namespace ClassSaver.Constants;

/// <summary>
/// All the byte markers.
/// </summary>
public enum Markers
{
    StartPrimitive = 8, // for fields & properties
    StartCollection = 9, // for ICollection
    StartSerializable = 10, // for ISerializable
    StartSection = 11, // for ClassSection
    StartClass = 12, // for any class/struct
    StartReference = 13, // for start of reference types
    ReferenceTo = 14, // to reference to a reference type
    EndScope = 15
}