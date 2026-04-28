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
    private const string XMLPath = "Config/ClassSaverConfig.xml";
    // markers section
    private const string XML_MarkersSectionName = "Markers";
    private const string XML_MarkersName = "name";
    private const string XML_MarkersValue = "value";
    // type map section
    private const string XML_TypeMapSectionName = "TypeMap";
    private const string XML_TypeMapName = "name";
    private const string XML_TypeMapValue = "value";
    #endregion
    
    // byte constants -- do not change bc it is important, rather add more & expand.
    public const byte StartEnumerable = 0x01;
    public const byte StartClass = 0x02;
    public const byte StartSection = 0x03;
    public const byte EndScope = 0x04;
    
    // byte constants for primitive data types
    const byte TypeBoolean = 0x11;
    const byte TypeByte = 0x12;
    const byte TypeSByte = 0x13;
    const byte TypeChar = 0x14;
    const byte TypeInt16 = 0x15;
    const byte TypeUInt16 = 0x16;
    const byte TypeInt32 = 0x17;
    const byte TypeUInt32 = 0x18;
    const byte TypeInt64 = 0x19;
    const byte TypeUInt64 = 0x20;
    const byte TypeSingle = 0x21;
    const byte TypeNull = 0x22;
    const byte TypeDouble = 0x23;
    const byte TypeIntPtr = 0x24;
    const byte TypeUIntPtr = 0x25;
    const byte TypeString = 0x26;
    const byte TypeDecimal = 0x27;
    
    private static readonly Dictionary<string, byte> _markerMap = new();
    private static readonly Dictionary<string, byte> _typeMap = new();

    static ClassSaverManager()
    {
        XDocument doc = XDocument.Load(XMLPath);
        if (doc.Root == null) throw new("Config/ClassSaverConfig.xml not found");
        
        // load data to marker map
        var markersSection = doc.Root.Element(XML_MarkersSectionName);

        if (markersSection == null) throw new ($"No such section '{XML_MarkersSectionName}' found in xml file");

        foreach (var marker in markersSection.Elements())
        {
            var markerName = marker.Attribute(XML_MarkersName).Value;
            var markerValue = (byte)int.Parse(marker.Attribute(XML_MarkersValue).Value);
            
            _markerMap.Add(markerName, markerValue);
        }
        
        // load data to type map
        var typeMapSection = doc.Root.Element(XML_TypeMapSectionName);
        
        if (typeMapSection == null) throw new($"No such section '{XML_TypeMapName}' found in xml file");

        foreach (var typeMap in typeMapSection.Elements())
        {
            var typeName = typeMap.Attribute(XML_TypeMapName).Value;
            var typeValue = (byte)int.Parse(typeMap.Attribute(XML_TypeMapValue).Value);
            
            _typeMap.Add(typeName, typeValue);
        }
    }
    
    /// <summary>
    /// Is the type we are dealing with a primitive?
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>true/false depending on if it's a primitive or not</returns>
    public static bool IsPrimitive(Type type)
    {
        return type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
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
        var type = data.GetType();

        if (data == null)
        {
            byteCode = TypeNull;
            return true;
        }
        
        if (!IsPrimitive(type)) return false;
        
        var typeName = type.Name;

        byteCode = typeName switch
        {
            ("Boolean") => TypeBoolean,
            ("Byte") => TypeByte,
            ("SByte") => TypeSByte,
            ("Char") => TypeChar,
            ("Int16") => TypeInt16,
            ("UInt16") => TypeUInt16,
            ("Int32") => TypeInt32,
            ("UInt32") => TypeUInt32,
            ("Int64") => TypeInt64,
            ("UInt64") => TypeUInt64,
            ("Single") => TypeSingle,
            ("Double") => TypeDouble,
            ("IntPtr") => TypeIntPtr,
            ("UIntPtr") => TypeUIntPtr,
            ("String") => TypeString,
            ("Decimal") => TypeDecimal,
            _ => byteCode
        };

        return true;
    }
}