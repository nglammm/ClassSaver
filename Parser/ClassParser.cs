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

    private SectionNumbers _currentSectionNumber;
    
    private ParseMode _parseMode;
    
    private object? _refObject;
    private bool _refHasParsedObj;
    
    private Dictionary<string, Type> _typeCacheMap = new();
    
    private Dictionary<int, object> _referenceMap = new();
    private Dictionary<int, Queue<Action<object>>> _waitingToBeReferenced = new();
    
    /// <summary>
    /// Path to the parent.
    /// </summary>
    public class ParentLocation
    {
        public object ParentObject;
        private FieldInfo? _fieldInfo;
        private PropertyInfo? _propertyInfo;

        public ParentLocation(object parentObj, FieldInfo field)
        {
            ParentObject = parentObj;
            _fieldInfo = field;
        }

        public ParentLocation(object parentObj, PropertyInfo property)
        {
            ParentObject = parentObj;
            _propertyInfo = property;
        }

        public void SetToParent(object value)
        {
            if (_fieldInfo != null) _fieldInfo.SetValue(ParentObject, value);
            else if (_propertyInfo != null) _propertyInfo.SetValue(ParentObject, value);
            else throw new($"No such field info or property info assigned.");
        }
    }
    
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
        
        _referenceMap.Clear();
        _waitingToBeReferenced.Clear();
        
        using var reader = new BinaryReader(stream);
        
        _currentSectionNumber = SectionNumbers.Header;
        _headerSection = ReadSection<HeaderSection>(SectionNumbers.Header, reader);
        
        _currentSectionNumber = SectionNumbers.Cache;
        _cacheSection = ReadSection<CacheSection>(SectionNumbers.Cache, reader);
        
        _currentSectionNumber = SectionNumbers.Data;
        return ReadSection<T>(SectionNumbers.Data, reader);
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
        
        _referenceMap.Clear();
        _waitingToBeReferenced.Clear();
        
        using var reader = new BinaryReader(stream);
        
        _currentSectionNumber = SectionNumbers.Header;
        _headerSection = ReadSection<HeaderSection>(SectionNumbers.Header, reader);
        
        _currentSectionNumber = SectionNumbers.Cache;
        _cacheSection = ReadSection<CacheSection>(SectionNumbers.Cache, reader);
        
        _currentSectionNumber = SectionNumbers.Data;
        return ReadSection(SectionNumbers.Data, reader);
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
        
        _referenceMap.Clear();
        _waitingToBeReferenced.Clear();
        
        using var reader = new BinaryReader(stream);
        
        _currentSectionNumber = SectionNumbers.Header;
        _headerSection = ReadSection<HeaderSection>(SectionNumbers.Header, reader);
        
        _currentSectionNumber = SectionNumbers.Cache;
        _cacheSection = ReadSection<CacheSection>(SectionNumbers.Cache, reader);
        
        _currentSectionNumber = SectionNumbers.Data;
        ReadSection<T>(SectionNumbers.Data, reader);
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
        
        _referenceMap.Clear();
        _waitingToBeReferenced.Clear();
        
        using var reader = new BinaryReader(stream);
        
        _currentSectionNumber = SectionNumbers.Header;
        _headerSection = ReadSection<HeaderSection>(SectionNumbers.Header, reader);
        
        _currentSectionNumber = SectionNumbers.Cache;
        _cacheSection = ReadSection<CacheSection>(SectionNumbers.Cache, reader);
        
        _currentSectionNumber = SectionNumbers.Data;
        ReadSection(SectionNumbers.Data, reader);
    }
    
    #endregion
    
    #endregion
    
    #region Read Section functions
    
    #region read section
    private T ReadSection<T>(SectionNumbers sectionNumber, BinaryReader reader) where T : new()
    {
        var startSectionByte = reader.ReadByte();
        if ((byte)Markers.StartSection != startSectionByte)
        {
            throw new($"Expected '{Markers.StartSection:x}', got '{startSectionByte:x}'.");
        }

        var sectionCode = reader.ReadInt32();
        if (sectionCode != (int)sectionNumber) throw new($"Expected section code '{sectionCode}', got '{sectionNumber}'.");
        
        switch (sectionNumber)
        {
            case SectionNumbers.Header:
                var header = ReadSectionHeader(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new("The expected end byte does not match") : Unsafe.As<HeaderSection, T>(ref header);
            case SectionNumbers.Cache:
                var cache = ReadSectionCache(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new("The expected end byte does not match") : Unsafe.As<CacheSection, T>(ref cache);
            case SectionNumbers.Data:
                return ReadSectionData<T>(reader);
            default:
                throw new Exception($"Unimplemented section type '{sectionNumber}'");
        }
    }

    private object ReadSection(SectionNumbers sectionNumber, BinaryReader reader)
    {
        var startSectionByte = reader.ReadByte();
        if ((byte)Markers.StartSection != startSectionByte)
        {
            throw new($"Expected '{Markers.StartSection:x}', got '{startSectionByte:x}'.");
        }

        var sectionCode = reader.ReadInt32();
        if (sectionCode != (int)sectionNumber) throw new($"Expected section code '{sectionCode}', got '{sectionNumber}'.");
        
        switch (sectionNumber)
        {
            case SectionNumbers.Header:
                var header = ReadSectionHeader(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false)
                    ? throw new("The expected end byte does not match")
                    : header;
            case SectionNumbers.Cache:
                var cache = ReadSectionCache(reader);
                return !IsByte(reader, (byte)Markers.EndScope, false)
                    ? throw new("The expected end byte does not match")
                    : cache;
            case SectionNumbers.Data:
                return ReadSectionData(reader);
            default:
                throw new Exception($"Unimplemented section type '{sectionNumber}'");
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
    
    private object? ParseNoCache(BinaryReader reader, ParentLocation? parent = null)
    {
        var varMarkerByte = reader.ReadByte();
        
        object? varItem;
        object? refObj = null;
        
        if (_currentSectionNumber == SectionNumbers.Data && !_refHasParsedObj)
        {
            refObj = _refObject;
            _refHasParsedObj = true;
        }

        switch (varMarkerByte)
        {
            case (byte)Markers.StartPrimitive:
                varItem = ReadPrimitiveNoCache(reader);
                break;
            case (byte)Markers.StartCollection:
                varItem = ReadCollectionNoCache(reader);
                break;
            case (byte)Markers.StartClass:
                varItem = ReadClassNoCache(reader, refObj, parent);
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
    private object? ReadClassNoCache(BinaryReader reader, object? objInstance = null, ParentLocation? parent = null)
    {
        object? outputClass;

        if (IsByte(reader, (byte)Markers.ReferenceTo, false))
        {
            
            var refCode = reader.ReadInt32();
            
            // if this reference code does not exist in the reference map yet,
            // we put it in the waiting queue until the reference code existed
            // which will clear the queue
            if (!_referenceMap.TryGetValue(refCode, out outputClass))
            {
                if (!_waitingToBeReferenced.ContainsKey(refCode)) _waitingToBeReferenced[refCode] = new();
                if (parent != null) _waitingToBeReferenced[refCode].Enqueue(parent.SetToParent);
                else throw new("Expected parent location yet no parent location found.");
            }

            return !IsByte(reader, (byte)Markers.EndScope, false) ? throw new($"Expected end byte.'") : outputClass;
        }

        reader.BaseStream.Position -= 1;
        
        if (!IsByte(reader, (byte)Markers.StartReference, false))
        {
            throw new($"Expected either start reference or reference to bytes, got none.");
        }
        
        var desiredRefCode = reader.ReadInt32();
        var type = GetTypeFromString(reader.ReadString());
        
        if (objInstance != null)
        {
            if (objInstance.GetType() != type)
                throw new(
                    $"Object instance param type of '{objInstance.GetType().Name}' passed in does not match passed in type '{type.Name}'"
                    );
            
            outputClass = objInstance;
        }
        else outputClass = Activator.CreateInstance(type);
            
        // preload all variables
        var fieldsMap = type.GetFields(Manager.BindingFlagsAll).ToDictionary(field => field.Name);
        var propertiesMap = type.GetProperties(Manager.BindingFlagsAll).ToDictionary(p => p.Name);
        var methodsMap = type.GetMethods(Manager.BindingFlagsAll).ToDictionary(method => method.Name);
        
        while (!IsByte(reader, (byte)Markers.EndScope))
        {
            ProcessVariableNoCache(reader, outputClass, fieldsMap, propertiesMap, methodsMap);
        }
        
        reader.BaseStream.Position += 1; // it is end scope byte here so pass it.
        
        // add to reference map
        _referenceMap.Add(desiredRefCode, outputClass);
        
        // and clear all the queue (if we have to)
        if (!_waitingToBeReferenced.TryGetValue(desiredRefCode, out var value)) return outputClass;
        
        while (value.Count > 0)
        {
            value.Dequeue().Invoke(outputClass);
        }
        
        _waitingToBeReferenced.Remove(desiredRefCode);
        return outputClass;
    }

    private (string, VariableTypes) GetVariableInfoNoCache(BinaryReader reader, bool fixStreamPos = true)
    {
        if (!IsByte(reader, (byte)ClassMarkers.StartVariable, false))
            throw new($"Expected start variable byte of '{ClassMarkers.StartVariable}' but didnt match.");

        var varTypeByte = reader.ReadByte();
        var varType = (VariableTypes)varTypeByte;
        
        var varName = reader.ReadString();

        if (fixStreamPos) reader.BaseStream.Position -= 3;

        return (varName, varType);
    }

    private void ProcessVariableNoCache(BinaryReader reader, object baseObj, Dictionary<string, FieldInfo> fieldMap, Dictionary<string, PropertyInfo> propertyMap, Dictionary<string, MethodInfo> methodMap)
    {
        var (varName, varType) = GetVariableInfoNoCache(reader, false);
        
        if (!IsByte(reader, (byte)ClassMarkers.StartVariableData, false))
            throw new($"Expected start variable data byte of '{ClassMarkers.StartVariableData}'.");
        
        // create new parent location
        ParentLocation? parentLoc;
        
        switch (varType)
        {
            case VariableTypes.Field:
                if (!fieldMap.TryGetValue(varName, out var field)) throw new($"No such field '{varName}' found.");
                parentLoc = new ParentLocation(baseObj, field);
                ProcessField(baseObj, methodMap, field, ParseNoCache(reader, parentLoc));
                break;
            case VariableTypes.Property:
                if (!propertyMap.TryGetValue(varName, out var property)) throw new($"No such property '{varName}' found.");
                parentLoc = new ParentLocation(baseObj, property);
                ProcessProperty(baseObj, methodMap, property, ParseNoCache(reader, parentLoc));
                break;
            default:
                throw new($"Unsupported variable type '{varType}'.");
        }
        
        if (!IsByte(reader, (byte)Markers.EndScope, false)) throw new($"Expected end byte value of '{Markers.EndScope}'.");
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
                Console.WriteLine($"Field {field.Name}'s attribute has an unknown method. Setting value anyway...");
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
                Console.WriteLine($"Property {property.Name}'s attribute has an unknown method. Setting value anyway...");
                return;
            }
            
            var res = (bool)method.Invoke(classObj, [propertyData]);

            if (!res) return;
        }
        
        property.SetValue(classObj, propertyData);
    }
    #endregion
}