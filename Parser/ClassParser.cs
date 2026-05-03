using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ClassSaver;

/// <summary>
/// Parses the class with given stream.
/// Only works if serialized with class 'ClassSerializer'.
/// </summary>
public class ClassParser
{
    public HeaderSection HeaderSection => _headerSection;
    public CacheSection CacheSection => _cacheSection;
    
    private HeaderSection? _headerSection;
    private CacheSection? _cacheSection;

    private object _refObject;
    private Type _refObjectType;
    
    private Dictionary<string, Type> _typeCacheMap = new();
    
    /// <summary>
    /// Parses the data stream and creates a <b>new</b> instance of the data on output.
    /// </summary>
    /// <param name="stream">The data stream</param>
    /// <typeparam name="T">The type expected to parse</typeparam>
    /// <returns></returns>
    public T Parse<T>(Stream stream) where T : new()
    {
        _refObject = null;
        
        using var reader = new BinaryReader(stream);
        
        _headerSection = ReadSection<HeaderSection>(ClassSection.Header, reader);
        _cacheSection = ReadSection<CacheSection>(ClassSection.Cache, reader);
        
        return ReadSection<T>(ClassSection.Data, reader);
    }
    
    /// <summary>
    /// [TODO] Parses to an existing object.
    /// </summary>
    /// <param name="obj">The object's reference</param>
    /// <param name="stream">The data stream</param>
    public void ParseTo<T>(ref T obj, Stream stream) where T : new()
    {
        _refObject = obj;
        _refObjectType = typeof(T);

        throw new NotImplementedException();
    }
    

    private T ReadSection<T>(ClassSection section, BinaryReader reader) where T : new()
    {
        var startSectionByte = reader.ReadByte();
        if (ClassSaverManager.GetMarkerByteCode("StartSection") != startSectionByte)
        {
            throw new($"Expected '{ClassSaverManager.GetMarkerByteCode("StartSection"):x}', got '{startSectionByte:x}'.");
        }

        var sectionCode = reader.ReadInt32();
        if (sectionCode != (int)section) throw new($"Expected section code '{sectionCode}', got '{section}'.");
        
        switch (section)
        {
            case ClassSection.Header:
                var header = ReadSectionHeader(reader);
                return !IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope"), false) ? throw new("The expected end byte does not match") : Unsafe.As<HeaderSection, T>(ref header);
            case ClassSection.Cache:
                var cache = ReadSectionCache(reader);
                return !IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope"), false) ? throw new("The expected end byte does not match") : Unsafe.As<CacheSection, T>(ref cache);
            case ClassSection.Data:
                return ReadSectionData<T>(reader);
            default:
                throw new Exception($"Unimplemented section type '{section}'");
        }
    }

    /// <summary>
    /// Checks if a binary reader's current byte is a byte
    /// we expect or not.
    /// </summary>
    /// <param name="reader">The binary reader</param>
    /// <param name="expectedByte">The byte we expect</param>
    /// <param name="fixStreamPos">Does the binary reader stream pos stays the same after comparing the byte? [default true]</param>
    /// <returns>True/False depending on if the byte matches or not</returns>
    private static bool IsByte(BinaryReader reader, byte expectedByte, bool fixStreamPos = true)
    {
        var readByte = reader.ReadByte();
        
        if (fixStreamPos) reader.BaseStream.Position -= 1;
        
        return readByte == expectedByte;
    }
    

    private HeaderSection ReadSectionHeader(BinaryReader reader)
    {
        return Parse<HeaderSection>(reader);
    }

    private CacheSection ReadSectionCache(BinaryReader reader)
    {
        return Parse<CacheSection>(reader);
    }

    private T ReadSectionData<T>(BinaryReader reader) where T : new()
    {
        return Parse<T>(reader);
    }

    private T Parse<T>(BinaryReader reader) where T : new()
    {
        return (T)Parse(reader);
    }
    
    /// <summary>
    /// Starts parsing at the start of any start byte.
    /// </summary>
    /// <param name="reader">Binary Reader instance</param>
    /// <returns>Object after parse</returns>
    private object Parse(BinaryReader reader)
    {
        return Parse(reader, out _);
    }
    
    private object Parse(BinaryReader reader, out string varName)
    {
        // variables data
        var varTypeByte = reader.ReadByte();
        varName = reader.ReadString();
        object varItem;
            
        // map to the correct section
        if (varTypeByte == ClassSaverManager.GetMarkerByteCode("StartCollection")) varItem = ReadCollection(reader);
        else if (varTypeByte == ClassSaverManager.GetMarkerByteCode("StartClass")) varItem = ReadClass(GetTypeFromString(reader.ReadString()), reader); // class type is assembly qualified
        else if (varTypeByte == ClassSaverManager.GetMarkerByteCode("StartSerializable")) varItem = ReadISerializable(reader);
        else if (varTypeByte == ClassSaverManager.GetMarkerByteCode("StartPrimitive")) varItem = ReadPrimitive(reader);
        else throw new($"Unsupported byte type '{varTypeByte}'.");
        
        // last byte is handled in those functions.
        
        return varItem;
    }

    private object ReadClass(Type tType, BinaryReader reader)
    {
        
        // preload all variables
        var fieldsMap = tType.GetFields(ClassSaverManager.BindingFlagsAll).ToDictionary(field => field.Name);
        var propertiesMap = tType.GetProperties(ClassSaverManager.BindingFlagsAll).ToDictionary(p => p.Name);
        
        // preparing the outputs
        var outputClass = Activator.CreateInstance(tType);

        while (!IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope")))
        {
            var varData = Parse(reader, out var varName);
            
            // set to variable
            if (fieldsMap.TryGetValue(varName, out var field)) field.SetValue(outputClass, varData);
            else if (propertiesMap.TryGetValue(varName, out var property)) property.SetValue(outputClass, varData);
            else throw new($"No such variable '{varName}' found in type '{tType.Name}'.");
        }
        
        reader.BaseStream.Position += 1; // it is end scope byte here so pass it.
        
        return outputClass;
    }

    private ICollection<object>? ReadCollection(BinaryReader reader)
    {
        var collectionTypeString = reader.ReadString();
        var collectionType = GetTypeFromString(collectionTypeString);
        
        var collectionLength = reader.ReadInt32();
        var outputCollection = Activator.CreateInstance(collectionType) as ICollection<object>;

        for (int i = 0; i < collectionLength; i++)
        {
            var collectionItem = Parse(reader);
            outputCollection.Add(collectionItem);
        }

        return !IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope"), false) ? throw new($"End byte scope not found.") : outputCollection;
    }

    private object ReadISerializable(BinaryReader reader)
    {
        var baseTypeName = reader.ReadString();
        var baseType = GetTypeFromString(baseTypeName);

        // add to cache
        _typeCacheMap.Add(baseTypeName, baseType);
        
        var baseObj = Activator.CreateInstance(baseType);
        if (baseObj is not ISerializable serializable)
        {
            throw new($"Type '{baseTypeName}' does not implement ISerializable.");
        }
        
        var parseObj = Parse(reader);
        serializable.Parse(parseObj);
        
        return !IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope"), false) ? throw new($"End byte scope not found.") : baseObj;
    }

    private object? ReadPrimitive(BinaryReader reader)
    {
        var typeCode = reader.ReadInt32();
        object output = null;
        
        switch ((TypeCode)typeCode)
        {
            case TypeCode.Boolean:
                output = reader.ReadBoolean();
                break;
            case TypeCode.Byte:
                output = reader.ReadByte();
                break;
            case TypeCode.SByte:
                output = reader.ReadSByte();
                break;
            case TypeCode.Char:
                output = reader.ReadChar();
                break;
            case TypeCode.Decimal:
                output = reader.ReadDecimal();
                break;
            case TypeCode.Double:
                output = reader.ReadDouble();
                break;
            case TypeCode.Single:
                output = reader.ReadSingle();
                break;
            case TypeCode.Int32:
                output = reader.ReadInt32();
                break;
            case TypeCode.UInt32:
                output = reader.ReadUInt32();
                break;
            case TypeCode.Int64:
                output = reader.ReadInt64();
                break;
            case TypeCode.UInt64:
                output = reader.ReadUInt64();
                break;
            case TypeCode.Int16:
                output = reader.ReadInt16();
                break;
            case TypeCode.UInt16:
                output = reader.ReadUInt16();
                break;
            case TypeCode.String:
                // binary writer already includes the length of string
                output = reader.ReadString();
                break;
            case TypeCode.Empty:
                // write nothing
                break;
            default:
                throw new($"Primitive data type '{(TypeCode)typeCode}' is not implemented.");
        }

        return !IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope"), false) ? throw new($"End byte scope not found") : output;
    }

    private Type GetTypeFromString(string typeName)
    {
        if (!_typeCacheMap.TryGetValue(typeName, out var type))
        {
            type = Type.GetType(typeName);
            if (type == null) throw new($"There is no such type named '{typeName}'");
            // add to cache
            _typeCacheMap.Add(typeName, type);
        }

        return type;
    }
}