using System.Reflection;
using ClassSaver.Constants;

namespace ClassSaver.Internal;

/// <summary>
/// Gateway between the serializer and the parser.
/// </summary>
public static class Manager
{
    public const BindingFlags BindingFlagsAll =  BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Dictionary<string, byte> _markersMap = new();
    private static Dictionary<string, byte> _primitiveDatatypesMap = new();
    private static Dictionary<string, byte> _collectionInterfacesMap = new();
    
    static Manager()
    {
        #region marker values cache
        // markers dict map
        var markerNames = Enum.GetNames<Markers>();
        var markerValues = Enum.GetValues<Markers>();

        for (var i = 0; i < markerNames.Length; i++)
        {
            var markerName = markerNames[i];
            var markerValue = markerValues[i];
            
            _markersMap.Add(markerName, (byte)markerValue);
        }
        #endregion
        
        #region primitive datatypes cache
        var primitiveNames = Enum.GetNames<PrimitiveDatatypes>();
        var primitiveValues = Enum.GetValues<PrimitiveDatatypes>();

        for (var i = 0; i < primitiveNames.Length; i++)
        {
            var primitiveName = primitiveNames[i];
            var primitiveValue = primitiveValues[i];
            
            _primitiveDatatypesMap.Add(primitiveName, (byte)primitiveValue);
        }
        #endregion

        #region collection interfaces cache
        var collectionInterfaceNames = Enum.GetNames<CollectionInterfaces>();
        var collectionInterfaceValues = Enum.GetValues<CollectionInterfaces>();

        for (var i = 0; i < collectionInterfaceNames.Length; i++)
        {
            var collectionInterfaceName = collectionInterfaceNames[i];
            var collectionInterfaceValue = collectionInterfaceValues[i];
            
            _collectionInterfacesMap.Add(collectionInterfaceName, (byte)collectionInterfaceValue);
        }
        #endregion
    }
    
    #region Primitives data type functions
    
    /// <summary>
    /// Checks if the type we are handling is ClassSaver's primitive.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True/false depending on if the type is a primitive or not.</returns>
    public static bool IsPrimitive(Type type)
    {
        return _primitiveDatatypesMap.ContainsKey(type.Name);
    }
    
    /// <summary>
    /// Gets the primitive byte code from type. This can't return byte code of null because
    /// we have no extra data about the object itself.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <param name="value">The byte value returned (if true)</param>
    /// <returns>True/false depending on if there is a primitive byte code for this.</returns>
    public static bool GetPrimitiveByteCode(Type type, out byte value)
    {
        if (_primitiveDatatypesMap.TryGetValue(type.Name, out value))
            return true;
        
        // find via interface
        foreach (var t in type.GetInterfaces())
        {
            if (_primitiveDatatypesMap.TryGetValue(t.Name, out value)) return true;
        }

        return false;
    }
    
    /// <summary>
    /// Gets the ClassSaver's primitive byte code from an object. (if the object is a primitive)
    /// </summary>
    /// <param name="data">The object to check</param>
    /// <param name="value">The byte value returned (discard if this function returns false).</param>
    /// <returns>True/false depending on if the data is a primitive or not.</returns>
    public static bool GetPrimitiveByteCode(object? data, out byte value)
    {
        if (data is not null) return GetPrimitiveByteCode(data.GetType(), out value);
        value = (byte)PrimitiveDatatypes.Null;
        return true;
    }
    
    /// <summary>
    /// Checks if this type is a specified ClassSaver's primitive data type or not.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <param name="primitiveType">The data type specified.</param>
    /// <returns>True/false depending on if the type is a specified ClassSaver's primitive data type.</returns>
    public static bool IsPrimitiveDatatypeOf(Type type, PrimitiveDatatypes primitiveType)
    {
        if (!IsPrimitive(type)) return false;
        
        var typeName = type.Name;
        return _primitiveDatatypesMap[typeName] == (byte)primitiveType;
    }
    
    #endregion
    
    /// <summary>
    /// Gets the collection interface's byte code with type.
    /// </summary>
    /// <param name="type">The type to get</param>
    /// <param name="value">The byte value out (0 if not found)</param>
    /// <returns>True/false depending on if the interface is found or not.</returns>
    public static bool GetCollectionInterfaceByteCode(Type type, out byte value)
    {
        var typeInterfaces = type.GetInterfaces();

        foreach (var typeInterface in typeInterfaces)
        {
            if (_collectionInterfacesMap.TryGetValue(typeInterface.Name, out value)) return true;
        }

        value = 0;
        return false;
    }
    
    public static bool IsCollectionInterface(Type type)
    {
        return _collectionInterfacesMap.ContainsKey(type.Name);
    }
}