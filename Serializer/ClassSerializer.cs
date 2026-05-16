using System.Collections;
using System.Reflection;
using ClassSaver.Internal;
using ClassSaver.Constants;
using ClassSaver.Structure;

namespace ClassSaver;

/// <summary>
/// Serializes a class from a given stream.
/// </summary>
/// <para>
/// <a href="https://github.com/nglammm/ClassSaver/wiki/2-%E2%80%90-Serializer">Documentation</a>
/// </para>
public class ClassSerializer
{
    private CacheMode _cacheMode;
    private Type? _currentSerializingType;
    private object? _currentSerializingObj;

    private Dictionary<object, int> _referenceMap = new();

    private Dictionary<Type, List<FieldInfo>> _fieldMap = new();
    private Dictionary<Type, List<PropertyInfo>> _propertyMap = new();
    private HashSet<Type> _processedTypes = new();

    private Sections _currentSection;
    
    private Action<object?, Type, BinaryWriter>? _writeMethod;
    
    #region keyword cache variables
    
    // all vars relating key word cache starts with prefix "_kwc"
    
    private Dictionary<string, int> _kwcCodeMap = new();
    private Dictionary<int, string> _kwcWordMap = new();
        
    #endregion
    
    #region public functions
    /// <summary>
    /// Serializes the desired object to the data stream.
    /// </summary>
    /// <param name="desiredObj">The desired object to parse</param>
    /// <param name="dataStream">The data stream to work with</param>
    /// <param name="cacheMode">The cache mode to serialize with</param>
    public void Serialize<T>(T desiredObj, Stream dataStream, CacheMode cacheMode = CacheMode.None) where T : new()
    {
        _cacheMode = cacheMode;
        _currentSerializingType = typeof(T);
        _currentSerializingObj = desiredObj;
        
        _currentSection = Sections.Header;
        
        _referenceMap.Clear();
        
        BuildVariableMap(typeof(CacheSection));
        BuildVariableMap(typeof(HeaderSection));
        BuildVariableMap(_currentSerializingType);

        using var writer = new BinaryWriter(dataStream);
        
        // write the first header
        WriteSection(Sections.Header, writer);
        
        // write the cache section
        WriteSection(Sections.Cache, writer);
        
        // write the third data section
        WriteSection(Sections.Data, writer);
    }
    
    /// <summary>
    /// Serializes the desired object to the data stream (without specifying type)
    /// </summary>
    /// <param name="desiredObj">The desired object to parse</param>
    /// <param name="dataStream">The data stream to work with</param>
    /// <param name="cacheMode">The cache mode to serialize with</param>
    public void Serialize(object desiredObj, Stream dataStream, CacheMode cacheMode = CacheMode.None)
    {
        _cacheMode = cacheMode;
        _currentSerializingType = desiredObj.GetType();
        _currentSerializingObj = desiredObj;
        
        _currentSection = Sections.Header;
        
        _referenceMap.Clear();
        
        BuildVariableMap(typeof(CacheSection));
        BuildVariableMap(typeof(HeaderSection));
        BuildVariableMap(_currentSerializingType);
        
        using var writer = new BinaryWriter(dataStream);
        
        // write the first header
        WriteSection(Sections.Header, writer);
        
        // write the cache section
        WriteSection(Sections.Cache, writer);
        
        // write the third data section
        WriteSection(Sections.Data, writer);
    }
    #endregion
    
    #region section write

    /// <summary>
    /// Used for writing desired sections.
    /// </summary>
    /// <param name="section">The section to write</param>
    /// <param name="writer">The binary writer param</param>
    /// <exception cref="ArgumentOutOfRangeException">Called if there is no such ClassSection or it is not implemented.</exception>
    private void WriteSection(Sections section, BinaryWriter writer)
    {
        // already written the byte code here.
        _currentSection = section;
        
        writer.Write((byte)Markers.StartSection);
        writer.Write((int)section);

        switch (section)
        {
            case Sections.Header:
                WriteSectionHeader(writer);
                break;
            case Sections.Cache:
                WriteSectionCache(writer);
                break;
            case Sections.Data:
                WriteSectionData(writer);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        writer.Write((byte)Markers.EndScope);
    }

    
    private void WriteSectionHeader(BinaryWriter writer)
    {
        // [TODO] make header section
        var headerSection = new HeaderSection();
        Serialize(headerSection, headerSection.GetType(), writer);
    }

    private void WriteSectionCache(BinaryWriter writer)
    {
        // [TODO] make cache section
        
        switch (_cacheMode)
        {
            case CacheMode.None:
                SetupNoCache();
                break;
            case CacheMode.Keyword:
                SetupKwc();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        var cacheSection = new CacheSection
        {
            CacheMode = (int)_cacheMode,
            KeywordCache_WordMap = _kwcWordMap
        };
        
        Serialize(cacheSection, cacheSection.GetType(), writer);
    }
    

    private void WriteSectionData(BinaryWriter writer)
    {
        // run the appropriate cache
        Serialize(_currentSerializingObj, _currentSerializingType, writer);
    }
    #endregion

    #region No Cache Functions
    
    #region on start

    private void SetupNoCache()
    {
        _writeMethod = SerializeNoCache;
    }
    
    #endregion
    
    #region Serializer manager
    /// <summary>
    /// Serializes the desired object and then tries to save it.
    /// </summary>
    /// <param name="desiredObj">The desired object to parse</param>
    /// <param name="objectType">The object's type to parse</param>
    /// <param name="objectVarName">The desired variable name to save with [optional].
    ///     <para>
    ///         If left empty string then it will save with the object's type's name.
    ///     </para>
    /// </param>
    /// <param name="writer">The writer object</param>
    private void SerializeNoCache(object? desiredObj, Type objectType, BinaryWriter writer)
    {
        // is a primitive data type
        if (Manager.IsPrimitive(objectType))
        {
            WritePrimitiveNoCache(desiredObj, writer);
            return;
        }
        
        // is Collection
        if (desiredObj is ICollection collection)
        {
            WriteCollectionNoCache(desiredObj.GetType(), collection, writer);
            return;
        }
        
        // is a class
        WriteClassNoCache(desiredObj, writer);
    }
    #endregion

    #region class writer
    private void WriteClassNoCache(object classData, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartClass);
        
        // quick exit if the object is already referenced
        if (_referenceMap.TryGetValue(classData, out var refCode))
        {
            // write the reference
            writer.Write((byte)Markers.ReferenceTo);
            writer.Write(refCode);
            writer.Write((byte)Markers.EndScope);
            
            return;
        }
        
        // define a new reference
        refCode = _referenceMap.Count;
        _referenceMap.Add(classData, refCode);
            
        writer.Write((byte)Markers.StartReference);
        writer.Write(refCode);
        
        var classType = classData.GetType();
        writer.Write(classType.AssemblyQualifiedName);

        // get fields & save
        if (_fieldMap.TryGetValue(classType, out var fields))
        {
            // process fields
            foreach (var field in fields)
            {
                WriteFieldNoCache(field, classData, writer);
            }
        }

        // get properties
        if (_propertyMap.TryGetValue(classType, out var properties))
        {
            // process properties
            foreach (var property in properties)
            {
                WritePropertyNoCache(property, classData, writer);
            }
        }

        writer.Write((byte)Markers.EndScope);
    }

    #region write fields and properties
    private void WriteFieldNoCache(FieldInfo field, object baseClass, BinaryWriter writer)
    {
        var fieldVal = field.GetValue(baseClass);
        if (fieldVal == null) return;
        
        writer.Write((byte)ClassMarkers.StartVariable);
        
        writer.Write((byte)VariableTypes.Field);
        writer.Write(field.Name);
        
        // write the data
        writer.Write((byte)ClassMarkers.StartVariableData);
        
        SerializeNoCache(fieldVal, field.FieldType, writer);
        writer.Write((byte)Markers.EndScope);
    }

    private void WritePropertyNoCache(PropertyInfo property, object baseClass, BinaryWriter writer)
    {
        var propertyVal = property.GetValue(baseClass);
        if (propertyVal == null) return; // skip for null values
        
        writer.Write((byte)ClassMarkers.StartVariable);
        
        writer.Write((byte)VariableTypes.Property);
        writer.Write(property.Name);
        
        // write the data
        writer.Write((byte)ClassMarkers.StartVariableData);
        
        SerializeNoCache(propertyVal, property.PropertyType, writer);
        writer.Write((byte)Markers.EndScope);
    }
    #endregion
    
    #endregion
    
    #region ISerializable write
    /// <summary>
    /// Writes the object that implements the ISerializable interface.
    /// </summary>
    /// <param name="baseType">The base type of the serializable</param>
    /// <param name="value">The object value that implements ISerializable</param>
    /// <param name="writer">The binary writer</param>
    private void WriteSerializableNoCache(Type baseType, ISerializable value, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartSerializable);
        writer.Write(baseType.AssemblyQualifiedName);
        
        var toSerialize = value.Serialize();
        SerializeNoCache(toSerialize, toSerialize.GetType(), writer);
        
        writer.Write((byte)Markers.EndScope);
    }
    #endregion
    
    #region Write primitive
    private void WritePrimitiveNoCache(object? data, BinaryWriter writer)
    {
        var dataType = data?.GetType();
        
        // check if serializable or not to write
        if (Manager.IsPrimitiveDatatypeOf(dataType, PrimitiveDatatypes.ISerializable))
        {
            WriteSerializableNoCache(dataType, data as ISerializable, writer);
            return;
        }
        
        // mark as variable
        writer.Write((byte)Markers.StartPrimitive);
        
        // write the data
        WritePrimitiveData(data, writer);
        writer.Write((byte)Markers.EndScope);
    }
    
    #endregion
    
    #region collection writes
    private void WriteCollectionNoCache(Type dataType, ICollection collection, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartCollection);
        writer.Write(dataType.AssemblyQualifiedName);
        
        // write all the params to this collection
        var genericArguments = dataType.GetGenericArguments();
        writer.Write(genericArguments.Length);
        
        foreach (var argType in genericArguments)
        {
            writer.Write(argType.AssemblyQualifiedName);
        }
        
        WriteCollectionData(collection, dataType, genericArguments, writer);

        writer.Write((byte)Markers.EndScope);
    }
    
    #endregion
    
    #endregion
    
    #region Keyword Cache functions
    
    #region on start
    
    #region generate cache
    private void SetupKwc()
    {
        _writeMethod = SerializeKwc;
        
        // get all the fields/properties and then
        // cache their field names/property names with some integer value to cache.
        
        // first make the cache
        _kwcCodeMap.Clear();
        _kwcWordMap.Clear();

        foreach (var (type, fields) in _fieldMap)
        {
            SaveFieldsKwc(type, fields);
        }

        foreach (var (type, properties) in _propertyMap)
        {
            SavePropertiesKwc(type, properties);
        }
    }
    #endregion
    
    #region save fields & properties
    private void SaveFieldsKwc(Type fromType, List<FieldInfo> fields)
    {
        // cache the from type
        CacheStringKwc(fromType.AssemblyQualifiedName);
        
        foreach (var field in fields)
        {
            var fieldName = field.Name;
            CacheStringKwc(fieldName);
        }
    }

    private void SavePropertiesKwc(Type fromType, List<PropertyInfo> properties)
    {
        CacheStringKwc(fromType.AssemblyQualifiedName);
        
        foreach (var property in properties)
        {
            var propertyName = property.Name;
            CacheStringKwc(propertyName);
        }
    }
    #endregion
    
    #region helper functions
    private void CacheStringKwc(string data)
    {
        if (_kwcCodeMap.ContainsKey(data)) return;
            
        var code = _kwcCodeMap.Count;
        _kwcCodeMap.Add(data, code);
        _kwcWordMap.Add(code, data);
    }
    #endregion
    
    #endregion
    
    #region serializer manager

    private void SerializeKwc(object? desiredObj, Type desiredType, BinaryWriter writer)
    {
        if (Manager.IsPrimitive(desiredType))
        {
            WritePrimitiveKwc(desiredObj, writer);
            return;
        }

        if (desiredObj is ICollection collection)
        {
            WriteCollectionKwc(desiredType, collection, writer);
            return;
        }
        
        WriteClassKwc(desiredObj, writer);
    }
    
    #endregion
    
    #region write class
    private void WriteClassKwc(object classData, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartClass);
        
        // quick exit if the object is already referenced
        if (_referenceMap.TryGetValue(classData, out var refCode))
        {
            // write the reference
            writer.Write((byte)Markers.ReferenceTo);
            writer.Write(refCode);
            writer.Write((byte)Markers.EndScope);
            
            return;
        }
        
        // define a new reference
        refCode = _referenceMap.Count;
        _referenceMap.Add(classData, refCode);
            
        writer.Write((byte)Markers.StartReference);
        writer.Write(refCode);
        
        var classType = classData.GetType();
        
        writer.Write(_kwcCodeMap[classType.AssemblyQualifiedName]); // write the code


        if (_fieldMap.TryGetValue(classType, out var fields))
        {
            foreach (var field in fields)
            {
                WriteFieldKwc(field, classData, writer);
            }
        }

        if (_propertyMap.TryGetValue(classType, out var properties))
        {
            foreach (var property in properties)
            {
                WritePropertyKwc(property, classData, writer);
            }
        }

        writer.Write((byte)Markers.EndScope);
    }
    
    #region write fields & properties

    private void WriteFieldKwc(FieldInfo field, object baseClass, BinaryWriter writer)
    {
        var fieldVal = field.GetValue(baseClass);
        if (fieldVal == null) return;
        
        writer.Write((byte)ClassMarkers.StartVariable);
        
        writer.Write((byte)VariableTypes.Field);
        writer.Write(_kwcCodeMap[field.Name]);
        
        // write the data
        writer.Write((byte)ClassMarkers.StartVariableData);
        
        SerializeKwc(fieldVal, field.FieldType, writer);
        writer.Write((byte)Markers.EndScope);
    }

    private void WritePropertyKwc(PropertyInfo property, object baseClass, BinaryWriter writer)
    {
        var propertyVal = property.GetValue(baseClass);
        if (propertyVal == null) return;
        
        writer.Write((byte)ClassMarkers.StartVariable);
        
        writer.Write((byte)VariableTypes.Property);
        writer.Write(_kwcCodeMap[property.Name]);
        
        writer.Write((byte)ClassMarkers.StartVariableData);
        
        SerializeKwc(propertyVal, property.PropertyType, writer);
        writer.Write((byte)Markers.EndScope);
    }
    #endregion
    
    #endregion
    
    #region write iserializable
    private void WriteSerializableKwc(Type baseType, ISerializable value, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartSerializable);
        writer.Write(_kwcCodeMap[baseType.AssemblyQualifiedName]);
        
        var toSerialize = value.Serialize();
        SerializeKwc(toSerialize, toSerialize.GetType(), writer);
        
        writer.Write((byte)Markers.EndScope);
    }
    #endregion
    
    #region write primitive

    private void WritePrimitiveKwc(object? data, BinaryWriter writer)
    {
        var dataType = data?.GetType();
        
        // check if serializable or not to write
        if (Manager.IsPrimitiveDatatypeOf(dataType, PrimitiveDatatypes.ISerializable))
        {
            WriteSerializableKwc(dataType, data as ISerializable, writer);
            return;
        }
        
        writer.Write((byte)Markers.StartPrimitive);
        WritePrimitiveData(data, writer);
        writer.Write((byte)Markers.EndScope);
    }
    
    #endregion
    
    #region collection write

    private void WriteCollectionKwc(Type dataType, ICollection collection, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartCollection);
        writer.Write(_kwcCodeMap[dataType.AssemblyQualifiedName]);
        
        // write all the params to this collection
        var genericArguments = dataType.GetGenericArguments();
        writer.Write(genericArguments.Length);
        
        foreach (var argType in genericArguments)
        {
            writer.Write(_kwcCodeMap[argType.AssemblyQualifiedName]);
        }
        
        WriteCollectionData(collection, dataType, genericArguments, writer);

        writer.Write((byte)Markers.EndScope);
    }
    
    #endregion
    
    #endregion
    
    #region General Functions
    
    #region serialize
    void Serialize(object desiredObj, Type desiredType, BinaryWriter writer)
    {
        // only serialize with cache when the section we are at is data.
        if (_currentSection != Sections.Data)
        {
            SerializeNoCache(desiredObj, desiredType, writer);
            return;
        }
        
        switch (_cacheMode)
        {
            case CacheMode.None: 
                SerializeNoCache(desiredObj, desiredType, writer); 
                break;
            case CacheMode.Keyword:
                SerializeKwc(desiredObj, desiredType, writer);
                break;
            default: 
                throw new ArgumentOutOfRangeException();
        }
    }
    #endregion
    
    #region build var map
    /// <summary>
    /// Maps all the fields/properties that is qualified to be serialized.
    /// </summary>
    /// <param name="type">Type to build map</param>
    private void BuildVariableMap(Type type)
    {
        if (!_processedTypes.Add(type)) return;

        // remap
        var availableFields = type.GetFields(Manager.BindingFlagsAll);

        foreach (var field in availableFields)
        {
            // add to the map if it satisfies criteria
            if (field.IsPublic && field.GetCustomAttribute<DoNotSerialize>(false) != null) continue;
            if (field.IsPrivate && field.GetCustomAttribute<ForceSerialize>(false) == null) continue;
            
            if (!_fieldMap.ContainsKey(type)) _fieldMap[type] = [];
            _fieldMap[type].Add(field);
            
            var fieldType = field.FieldType;
            if (!(Manager.IsPrimitive(fieldType) &&
                !Manager.IsPrimitiveDatatypeOf(fieldType, PrimitiveDatatypes.ISerializable)))
            {
                // we need to build variable map for those types too
                BuildVariableMap(fieldType);
            }
        }
        
        var availableProperties = type.GetProperties(Manager.BindingFlagsAll);
        foreach (var property in availableProperties)
        {
            var isPublic = property is { CanRead: true, CanWrite: true };

            if (isPublic && property.GetCustomAttribute<DoNotSerialize>(false) != null) continue;
            if (!isPublic && property.GetCustomAttribute<ForceSerialize>(false) == null) continue;
            
            if (!_propertyMap.ContainsKey(type)) _propertyMap[type] = [];
            _propertyMap[type].Add(property);

            var propertyType = property.PropertyType;
            if (!(Manager.IsPrimitive(propertyType) &&
                  !Manager.IsPrimitiveDatatypeOf(propertyType, PrimitiveDatatypes.ISerializable)))
            {
                BuildVariableMap(propertyType);
            }
        }
        
        var genericArguments = type.GetGenericArguments();

        foreach (var argType in genericArguments)
        {
            BuildVariableMap(argType);
        }
    }
    #endregion
    
    #region general write methods
    
    #region write primitive data
    
    /// <summary>
    /// Writes the primitive data type value (excluding ISerializable because it got its own function).
    /// </summary>
    /// <param name="data">The data to write</param>
    /// <param name="writer">The binary writer</param>
    /// <exception cref="Exception">Primitive data type not implemented yet.</exception>
    private void WritePrimitiveData(object data, BinaryWriter writer)
    {
        if (!Manager.GetPrimitiveByteCode(data, out var typeByte)) throw new($"No such data type {data.GetType().Name}.");
        
        writer.Write(typeByte);

        switch (typeByte)
        {
            case (byte)PrimitiveDatatypes.Boolean:
                writer.Write((bool)data);
                break;
            case (byte)PrimitiveDatatypes.Byte:
                writer.Write((byte)data);
                break;
            case (byte)PrimitiveDatatypes.SByte:
                writer.Write((sbyte)data);
                break;
            case (byte)PrimitiveDatatypes.Char:
                writer.Write((char)data);
                break;
            case (byte)PrimitiveDatatypes.Decimal:
                writer.Write((decimal)data);
                break;
            case (byte)PrimitiveDatatypes.Double:
                writer.Write((double)data);
                break;
            case (byte)PrimitiveDatatypes.Single:
                writer.Write((float)data);
                break;
            case (byte)PrimitiveDatatypes.Int32:
                writer.Write((int)data);
                break;
            case (byte)PrimitiveDatatypes.UInt32:
                writer.Write((uint)data);
                break;
            case (byte)PrimitiveDatatypes.Int64:
                writer.Write((long)data);
                break;
            case (byte)PrimitiveDatatypes.UInt64:
                writer.Write((ulong)data);
                break;
            case (byte)PrimitiveDatatypes.Int16:
                writer.Write((short)data);
                break;
            case (byte)PrimitiveDatatypes.UInt16:
                writer.Write((ushort)data);
                break;
            case (byte)PrimitiveDatatypes.String:
                writer.Write((string)data);
                break;
            case (byte)PrimitiveDatatypes.Null:
                // write nothing for null
                break;
            default:
                throw new($"Unimplemented data type {data.GetType().Name}.");
        }
    }
    #endregion

    private void WriteCollectionData(ICollection collection, Type collectionType, Type[] genericArguments, BinaryWriter writer)
    {
        if (!Manager.GetCollectionInterfaceByteCode(collectionType, out var collectionByteCode))
            throw new($"Can't find collection byte code for {collectionType.Name}");
        
        writer.Write(collectionByteCode);
        
        var collectionCount = collection.Count;
        writer.Write(collectionCount);
        
        switch (collectionByteCode)
        {
            case (byte)CollectionInterfaces.IList:
                WriteIList(collection as IList, genericArguments, writer, _writeMethod);
                break;
            case (byte)CollectionInterfaces.IDictionary:
                WriteIDictionary(collection as IDictionary, genericArguments, writer, _writeMethod);
                break;
            default:
                throw new("Unsupported parsing ICollection type " + collection.GetType().Name);
        }
    }
    
    #region write ilist
    private void WriteIList(IList? data, Type[] genericArgs, BinaryWriter writer, Action<object?, Type, BinaryWriter>? writeMethod)
    {
        if (data == null) return;
        if (writeMethod == null) throw new("No write method found.");
        
        if (genericArgs.Length != 1)
            throw new($"Passed in generic arguments size is not '1'.");
        
        
        
        // serialize each element
        foreach (var item in data)
        {
            writer.Write((byte)CollectionMarkers.IListElementStart);
            writeMethod.Invoke(item, genericArgs[0], writer);
            writer.Write((byte)Markers.EndScope);
        }
    }
    #endregion
       
    #region write IDictionary
    private void WriteIDictionary(IDictionary? data, Type[] genericArgs, BinaryWriter writer, Action<object?, Type, BinaryWriter>? writeMethod)
    {
        if (data == null) return;
        if (writeMethod == null) throw new("No write method found.");
        
        // generic args array MUST be 2
        if (genericArgs.Length != 2)
            throw new($"Passed in generic arguments size is not '2'.");
        
        foreach (DictionaryEntry item in data)
        {
            // write key
            writer.Write((byte)CollectionMarkers.IDictionaryKeyStart);
            writeMethod.Invoke(item.Key, genericArgs[0], writer);
            writer.Write((byte)Markers.EndScope);
            
            // write value
            writer.Write((byte)CollectionMarkers.IDictionaryValueStart);
            writeMethod.Invoke(item.Value, genericArgs[1], writer);
            writer.Write((byte)Markers.EndScope);
        }
    }
    #endregion
    
    #endregion
    
    #endregion
}