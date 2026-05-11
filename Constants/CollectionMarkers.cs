namespace ClassSaver.Constants;

/// <summary>
/// Marker constants for ICollection.
/// </summary>
public enum CollectionMarkers
{
    IDictionaryKeyStart = 0x40,
    IDictionaryValueStart = 0x41,
    IListElementStart = 0x42,
    // end scope already exists in Markers.EndScope
}