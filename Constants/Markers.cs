namespace ClassSaver.Constants;

/// <summary>
/// All the byte markers.
/// </summary>
public enum Markers
{
    StartVariable = 1, // for fields & properties
    StartCollection = 2, // for ICollection
    StartSerializable = 3, // for ISerializable
    StartSection = 4, // for ClassSection
    StartClass = 5, // for any class/struct
    EndScope = 6
}