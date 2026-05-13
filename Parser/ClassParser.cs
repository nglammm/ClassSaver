using System.Collections;
using System.Reflection;
using ClassSaver.Internal;
using ClassSaver.Constants;
using System.Runtime.CompilerServices;
using ClassSaver.Parser;
using ClassSaver.Parser.ParseTo;
using ClassSaver.Structure;

namespace ClassSaver;

/// <summary>
/// Parses the class with given stream.
/// Only works if serialized with class 'ClassSerializer'.
/// </summary>
public class ClassParser
{
    /// <summary>
    /// The header section of the data parsed.
    /// </summary>
    public HeaderSection? HeaderSection => _headerSection;
    
    /// <summary>
    /// The cache section of the data parsed.
    /// </summary>
    public CacheSection? CacheSection => _cacheSection;
    
    private HeaderSection? _headerSection;
    private CacheSection? _cacheSection;

    private Sections _currentSection;
    
    private ParseMode _parseMode;
    
    private object? _refObject;
    private bool _refHasParsedObj = false;
    
    private Dictionary<string, Type> _typeCacheMap = new();
    
    #region public parse functions
    
    #region normal parse
    
    /// <summary>
    /// Parses the data stream and creates a <b>new</b> instance of the data on output.
    /// </summary>
    /// <param name="stream">The data stream</param>
    /// <typeparam name="T">The type expected to parse</typeparam>
    /// <returns>The object parsed and returned as new</returns>
    public T Parse<T>(Stream stream) where T : new()
    {
        _parseMode = ParseMode.NewObject;
        
        _refObject = null;
        _refHasParsedObj = false;
        
        using var reader = new BinaryReader(stream);
        
        _currentSection = Sections.Header;
        _headerSection = ReadSection<HeaderSection>(Sections.Header, reader);
        
        _currentSection = Sections.Cache;
        _cacheSection = ReadSection<CacheSection>(Sections.Cache, reader);
        
        _currentSection = Sections.Data;
        return ReadSection<T>(Sections.Data, reader);
    }
    
    /// <summary>
    /// It is safer if you run Parse with type reference overload
    /// as the parser knows how to process the object parsed.
    /// <para>
    /// Parses the data stream and creates a <b>new</b> instance of the data on output.
    /// </para>
    /// </summary>
    /// <param name="stream">The data stream</param>
    /// <returns>The object from the stream.</returns>
    public object Parse(Stream stream)
    {
        _parseMode = ParseMode.NewObject;
        
        _refObject = null;
        _refHasParsedObj = false;
        
        using var reader = new BinaryReader(stream);
        
        _currentSection = Sections.Header;
        _headerSection = ReadSection<HeaderSection>(Sections.Header, reader);
        
        _currentSection = Sections.Cache;
        _cacheSection = ReadSection<CacheSection>(Sections.Cache, reader);
        
        _currentSection = Sections.Data;
        return ReadSection(Sections.Data, reader);
    }
    
    #endregion
    
    #region parse to
    
    /// <summary>
    /// Parses to an object instance instead of creating a new instance.
    /// <para>
    /// Recommended to use this function instead of the alternative overload because it is safer.
    /// </para>
    /// </summary>
    /// <param name="obj">The object's reference</param>
    /// <param name="stream">The data stream</param>
    public void ParseTo<T>(T obj, Stream stream) where T : new()
    {
        _parseMode = ParseMode.ToReference;
        
        _refObject = obj;
        _refHasParsedObj = false;
        
        using var reader = new BinaryReader(stream);
        
        _currentSection = Sections.Header;
        _headerSection = ReadSection<HeaderSection>(Sections.Header, reader);
        
        _currentSection = Sections.Cache;
        _cacheSection = ReadSection<CacheSection>(Sections.Cache, reader);
        
        _currentSection = Sections.Data;
        ReadSection<T>(Sections.Data, reader);
    }
    
    /// <summary>
    /// Parses to an object instance instead of creating a new instance.
    /// <para>
    /// Do note that this function is equivalent to the normal Parse() function
    /// for structs because they are value types instead of reference types like
    /// classes do.
    /// </para>
    /// </summary>
    /// <param name="toObj">The object's reference</param>
    /// <param name="stream">The data stream</param>
    public void ParseTo(object toObj, Stream stream)
    {
        _parseMode = ParseMode.ToReference;
        
        _refObject = toObj;
        _refHasParsedObj = false;
        
        using var reader = new BinaryReader(stream);
        
        _currentSection = Sections.Header;
        _headerSection = ReadSection<HeaderSection>(Sections.Header, reader);
        
        _currentSection = Sections.Cache;
        _cacheSection = ReadSection<CacheSection>(Sections.Cache, reader);
        
        _currentSection = Sections.Data;
        ReadSection(Sections.Data, reader);
    }
    
    #endregion
    
    #endregion
    
    #region Read Section functions
    
    #region read section
    private T ReadSection<T>(Sections section, BinaryReader reader) where T : new()
    {
        var startSectionByte = reader.ReadByte();
        if ((byte)Markers.StartSection != startSectionByte)
        {
            throw new($"Expected '{Markers.StartSection:x}', got '{startSectionByte:x}'.");
        }

        var sectionCode = reader.ReadInt32();
        if (sectionCode != (int)section) throw new($"Expected section code '{sectionCode}', got '{section}'.");
        
        switch (section)
        {
            case Sections.Header:
                var header = ReadSectionHeader(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new("The expected end byte does not match") : Unsafe.As<HeaderSection, T>(ref header);
            case Sections.Cache:
                var cache = ReadSectionCache(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new("The expected end byte does not match") : Unsafe.As<CacheSection, T>(ref cache);
            case Sections.Data:
                return ReadSectionData<T>(reader);
            default:
                throw new Exception($"Unimplemented section type '{section}'");
        }
    }

    private object ReadSection(Sections section, BinaryReader reader)
    {
        var startSectionByte = reader.ReadByte();
        if ((byte)Markers.StartSection != startSectionByte)
        {
            throw new($"Expected '{Markers.StartSection:x}', got '{startSectionByte:x}'.");
        }

        var sectionCode = reader.ReadInt32();
        if (sectionCode != (int)section) throw new($"Expected section code '{sectionCode}', got '{section}'.");
        
        switch (section)
        {
            case Sections.Header:
                var header = ReadSectionHeader(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false)
                    ? throw new("The expected end byte does not match")
                    : header;
            case Sections.Cache:
                var cache = ReadSectionCache(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false)
                    ? throw new("The expected end byte does not match")
                    : cache;
            case Sections.Data:
                return ReadSectionData(reader);
            default:
                throw new Exception($"Unimplemented section type '{section}'");
        }
    }
    
    #endregion
    
    #region understanding each section
    
    private HeaderSection ReadSectionHeader(BinaryReader reader) => Parse<HeaderSection>(reader);
    private CacheSection ReadSectionCache(BinaryReader reader) => Parse<CacheSection>(reader);
    private T ReadSectionData<T>(BinaryReader reader) where T : new() => Parse<T>(reader);

    private object ReadSectionData(BinaryReader reader)
    {
        return Parse(reader);
    }
    
    #endregion
    
    #endregion

    #region initial parse functions
    // the main parse function
    private T Parse<T>(BinaryReader reader) where T : new()
    {
        return (T)Parse(reader);
    }

    private object? Parse(BinaryReader reader)
    {
        if (_cacheSection == null)
        {
            return ParseNoCache(reader);
        }

        switch (_cacheSection.CacheMode)
        {
            case (int)CacheMode.None:
                return ParseNoCache(reader);
            default:
                throw new NotImplementedException();
        }
    }
    #endregion
    
    #region No cache functions
    
    #region general functions
    
    /// <summary>
    /// Starts parsing at the start of any start byte.
    /// </summary>
    /// <param name="reader">Binary Reader instance</param>
    /// <returns>Object after parse</returns>
    private object? ParseNoCache(BinaryReader reader)
    {
        return ParseNoCache(reader, out _);
    }
    
    private object? ParseNoCache(BinaryReader reader, out string varName)
    {
        var varMarkerByte = reader.ReadByte();
        varName = reader.ReadString();
        object? varItem;

        object? refObj = null;
        
        if (_currentSection == Sections.Data && !_refHasParsedObj)
        {
            refObj = _refObject;
            _refHasParsedObj = true;
        }

        switch (varMarkerByte)
        {
            case (byte)Markers.StartVariable:
                varItem = ReadPrimitiveNoCache(reader);
                break;
            case (byte)Markers.StartCollection:
                varItem = ReadCollectionNoCache(reader);
                break;
            case (byte)Markers.StartClass:
                varItem = ReadClassNoCache(GetTypeFromString(reader.ReadString()), reader, refObj);
                break;
            case (byte)Markers.StartSerializable:
                varItem = ReadISerializableNoCache(reader);
                break;
            default:
                throw new($"Unsupported byte type '{varMarkerByte}'.");
        }
        
        // last byte is handled in those functions.
        return varItem;
    }
    #endregion
    
    #region read class
    private object? ReadClassNoCache(Type tType, BinaryReader reader, object? objInstance = null)
    {
        // preload all variables
        var fieldsMap = tType.GetFields(Manager.BindingFlagsAll).ToDictionary(field => field.Name);
        var propertiesMap = tType.GetProperties(Manager.BindingFlagsAll).ToDictionary(p => p.Name);
        var methodsMap = tType.GetMethods(Manager.BindingFlagsAll).ToDictionary(method => method.Name);
        
        // preparing the outputs
        object? outputClass;

        if (objInstance != null)
        {
            if (objInstance.GetType() != tType)
                throw new(
                    $"Object instance param type of '{objInstance.GetType().Name}' passed in does not match passed in type '{tType.Name}'");
            
            outputClass = objInstance;
        }
        else outputClass = Activator.CreateInstance(tType);

        if (outputClass == null) throw new($"No such object '{tType.Name}'.");

        while (!IsByte(reader, (byte)Markers.EndScope))
        {
            var varData = ParseNoCache(reader, out var varName);
            
            // set to variable
            if (fieldsMap.TryGetValue(varName, out var field)) ProcessField(outputClass, methodsMap, field, varData);
            else if (propertiesMap.TryGetValue(varName, out var property)) ProcessProperty(outputClass, methodsMap, property, varData);
            else throw new($"No such variable '{varName}' found in type '{tType.Name}'.");
        }
        
        reader.BaseStream.Position += 1; // it is end scope byte here so pass it.
        
        return outputClass;
    }
    #endregion

    #region read collection
    private object? ReadCollectionNoCache(BinaryReader reader)
    {
        var collectionTypeString = reader.ReadString();
        var baseCollectionType = GetTypeFromString(collectionTypeString);
        
        // read and gets all the generic type args
        var genericArgumentLength = reader.ReadInt32();
        var genericArguments = new Type[genericArgumentLength];

        for (var i = 0; i < genericArguments.Length; i++)
        {
            var genericType = GetTypeFromString(reader.ReadString());
            genericArguments[i] = genericType;
        }

        var collectionByte = reader.ReadByte();
        
        object? outputCollection;

        switch (collectionByte)
        {
            case (byte)CollectionInterfaces.IList:
                outputCollection = ReadIListNoCache(baseCollectionType, genericArguments, reader);
                break;
            case (byte)CollectionInterfaces.IDictionary:
                outputCollection = ReadIDictionaryNoCache(baseCollectionType, genericArguments, reader);
                break;
            default:
                throw new("Unsupported parsing ICollection type " + baseCollectionType.Name);
        }

        return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new($"End byte scope not found.") : outputCollection;
    }

    private object ReadIListNoCache(Type baseListType, Type[] genericTypes, BinaryReader reader)
    {
        var collectionCount = reader.ReadInt32();
        var outputBaseType = GetTypeFromString(baseListType.AssemblyQualifiedName);
        var outputType = outputBaseType.MakeGenericType(genericTypes);
        
        var outputCollection = Activator.CreateInstance(outputType);

        if (outputCollection is not IList iList) throw new($"Object passed in is not an IList.");

        for (var i = 0; i < collectionCount; i++)
        {
            var startElementByte = reader.ReadByte();
            if (startElementByte != (byte)CollectionMarkers.IListElementStart)
                throw new($"Expected start byte for ilist, got {startElementByte:x}.");
            
            iList.Add(ParseNoCache(reader));
            
            var endScopeByte = reader.ReadByte();
            if (endScopeByte != (byte)Markers.EndScope) 
                throw new($"End byte scope not found. Got : {endScopeByte:x}.");
        }

        return iList;
    }

    
    private object ReadIDictionaryNoCache(Type baseDictionaryType, Type[] genericTypes, BinaryReader reader)
    {
        var collectionCount = reader.ReadInt32();
        
        var outputBaseType = baseDictionaryType.GetGenericTypeDefinition();
        var outputType = outputBaseType.MakeGenericType(genericTypes);
        
        var outputCollection = Activator.CreateInstance(outputType);
        if (outputCollection is not IDictionary iDictionary) throw new($"Object passed in is not an IDictionary.");

        for (var i = 0; i < collectionCount; i++)
        {
            var startKeyByte = reader.ReadByte();
            if (startKeyByte != (byte)CollectionMarkers.IDictionaryKeyStart)
                throw new($"Expected start byte for IDictionary's key start, got {startKeyByte:x}.");

            var key = ParseNoCache(reader);
            
            var endKeyByte = reader.ReadByte();
            if (endKeyByte != (byte)Markers.EndScope)
                throw new($"Expected end byte for end scope, got {endKeyByte:x} instead.");
            
            var startValueByte = reader.ReadByte();
            if (startValueByte != (byte)CollectionMarkers.IDictionaryValueStart)
                throw new($"Expected start byte for IDictionary's value start, got {startValueByte:x}.");
            
            var value = ParseNoCache(reader);
            
            var endValueByte = reader.ReadByte();
            if (endValueByte != (byte)Markers.EndScope)
                throw new($"Expected end byte for end scope, got {endValueByte:x} instead.");
            
            iDictionary.Add(key, value);
        }
        
        return iDictionary;
    }
    #endregion
    
    #region read iserializable
    private object ReadISerializableNoCache(BinaryReader reader)
    {
        var baseTypeName = reader.ReadString();
        var baseType = GetTypeFromString(baseTypeName);
        
        var baseObj = Activator.CreateInstance(baseType);
        if (baseObj is not ISerializable serializable)
        {
            throw new($"Type '{baseTypeName}' does not implement ISerializable.");
        }
        
        var parseObj = ParseNoCache(reader);
        serializable.Parse(parseObj);
        
        return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new($"End byte scope not found.") : baseObj;
    }
    #endregion
    
    #region read primitive datatypes
    private object? ReadPrimitiveNoCache(BinaryReader reader)
    {
        var typeByte = reader.ReadByte();
        object? output = null;

        switch (typeByte)
        {
            case (byte)PrimitiveDatatypes.Boolean:
                output = reader.ReadBoolean();
                break;
            case (byte)PrimitiveDatatypes.Byte:
                output = reader.ReadByte();
                break;
            case (byte)PrimitiveDatatypes.SByte:
                output = reader.ReadSByte();
                break;
            case (byte)PrimitiveDatatypes.Int16:
                output = reader.ReadInt16();
                break;
            case (byte)PrimitiveDatatypes.UInt16:
                output = reader.ReadUInt16();
                break;
            case (byte)PrimitiveDatatypes.Int32:
                output = reader.ReadInt32();
                break;
            case (byte)PrimitiveDatatypes.UInt32:
                output = reader.ReadUInt32();
                break;
            case (byte)PrimitiveDatatypes.Int64:
                output = reader.ReadInt64();
                break;
            case (byte)PrimitiveDatatypes.UInt64:
                output = reader.ReadUInt64();
                break;
            case (byte)PrimitiveDatatypes.Single:
                output = reader.ReadSingle();
                break;
            case (byte)PrimitiveDatatypes.Double:
                output = reader.ReadDouble();
                break;
            case (byte)PrimitiveDatatypes.Decimal:
                output = reader.ReadDecimal();
                break;
            case (byte)PrimitiveDatatypes.String:
                output = reader.ReadString();
                break;
            case (byte)PrimitiveDatatypes.Null:
                // read nothing
                break;
            default:
                throw new($"Unimplemented data type to parse: '{typeByte:x}'.");
        }

        return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new($"End byte scope not found") : output;
    }
    #endregion
    
    #endregion
    
    #region general functions
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
    
    
    private void ProcessField(object classObj, Dictionary<string, MethodInfo> methodMap, FieldInfo field, object fieldData)
    {
        var doNotOverrideAttr = field.GetCustomAttribute<DoNotOverride>();
        if (doNotOverrideAttr != null)
        {
            var checkFunc = doNotOverrideAttr.FuncCheckName;
            if (!methodMap.TryGetValue(checkFunc, out var method))
            {
                Console.Out.WriteLine($"Field {field.Name}'s attribute has an unknown method. Setting value anyway...");
                field.SetValue(classObj, fieldData);
                return;
            }
            
            var res = (bool)method.Invoke(classObj, [fieldData]);

            if (!res) return;
        }
        
        field.SetValue(classObj, fieldData);
    }
    
    private void ProcessProperty(object classObj, Dictionary<string, MethodInfo> methodMap, PropertyInfo property, object propertyData)
    {
        var doNotOverrideAttr = property.GetCustomAttribute<DoNotOverride>();
        if (doNotOverrideAttr != null)
        {
            var checkFunc = doNotOverrideAttr.FuncCheckName;
            if (!methodMap.TryGetValue(checkFunc, out var method))
            {
                if (string.IsNullOrEmpty(checkFunc)) return;
                Console.Out.WriteLine($"Property {property.Name}'s attribute has an unknown method. Setting value anyway...");
                return;
            }
            
            var res = (bool)method.Invoke(classObj, [propertyData]);

            if (!res) return;
        }
        
        property.SetValue(classObj, propertyData);
    }
    #endregion
}