using System.Reflection;
using System.Xml.Linq;

namespace ClassSaver;

/// <summary>
/// Gateway between the serializer and the parser.
/// </summary>
public static class ClassSaverManager
{
    public const BindingFlags BindingFlagsAll =  BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    
    #region XML Constants
    private const string XML_Path = "Config/ClassSaverConfig.xml";
    // markers section
    private const string XML_MarkersSectionName = "Markers";
    private const string XML_MarkersName = "name";
    private const string XML_MarkersValue = "value";
    private const string XML_MarkersByteType = "byteType";
    // type map section
    private const string XML_TypeMapSectionName = "TypeMap";
    private const string XML_TypeMapName = "name";
    private const string XML_TypeMapValue = "code";
    private const string XML_TypeType = "type";
    #endregion
    
    private static readonly Dictionary<string, (byte, string)> _markerMap = new();
    private static readonly Dictionary<string, (byte, string)> _typeMap = new();

    static ClassSaverManager()
    {
        var doc = XDocument.Load(XML_Path);
        if (doc.Root == null) throw new("Config/ClassSaverConfig.xml not found");
        
        // load data to marker map
        var markersSection = doc.Root.Element(XML_MarkersSectionName);

        if (markersSection == null) throw new ($"No such section '{XML_MarkersSectionName}' found in xml file");

        foreach (var marker in markersSection.Elements())
        {
            var markerName = marker.Attribute(XML_MarkersName).Value;
            var markerValue = (byte)int.Parse(marker.Attribute(XML_MarkersValue).Value);
            var markerType = marker.Attribute(XML_MarkersByteType).Value;
            
            _markerMap.Add(markerName, (markerValue, markerType));
        }
        
        // load data to type map
        var typeMapSection = doc.Root.Element(XML_TypeMapSectionName);
        
        if (typeMapSection == null) throw new($"No such section '{XML_TypeMapName}' found in xml file");

        foreach (var typeMap in typeMapSection.Elements())
        {
            var typeName = typeMap.Attribute(XML_TypeMapName).Value;
            var typeValue = (byte)int.Parse(typeMap.Attribute(XML_TypeMapValue).Value);
            var typeType = typeMap.Attribute(XML_TypeType).Value;
            
            _typeMap.Add(typeName, (typeValue, typeType));
        }
    }
    
    /// <summary>
    /// Is the type we are dealing with a primitive? <b>A type that implements
    /// ISerializable is also considered a primitive</b>
    /// <para>
    /// Refer to the ClassSaverConfig.xml for what is considered a primitive.
    /// </para>
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>true/false depending on if it's a primitive or not</returns>
    public static bool IsPrimitive(Type type)
    {
        return _typeMap.ContainsKey(type.Name);
    }
    
    /// <summary>
    /// Returns the marker byte with given marker name.
    /// </summary>
    /// <param name="markerName">The marker name.</param>
    /// <returns>The marker byte code, will return error instead if not found.</returns>
    public static byte GetMarkerByteCode(string markerName)
    {
        return _markerMap[markerName].Item1;
    }
    
    /// <summary>
    /// Retrieves the byte code of the appropriate data for primitive data types.
    /// </summary>
    /// <param name="data">The data to check</param>
    /// <param name="byteCode">The byte code returned</param>
    /// <returns>True if we successfully found byte code that is appropriate to the primitive data type.</returns>
    public static bool GetPrimitiveByteCode(object data, out byte byteCode)
    {
        byteCode = 0;
        var dataType = data.GetType();
        var dataTypeName = dataType.Name;

        if (!_typeMap.TryGetValue(dataTypeName, out var typeData)) return false;
        
        byteCode = typeData.Item1;
        return true;
    }
}