using System.Collections;
using System.Reflection;
using ClassSaver.Internal;
using ClassSaver.Constants;
using ClassSaver.Structure;

namespace ClassSaver;

/// <summary>
/// Serializes a class from a given stream.
/// </summary>
public class ClassSerializer
{
    private CacheMode _cacheMode;
    private Type? _currentSerializingType;
    private object? _currentSerializingObj;

    private Dictionary<object, int> _referenceMap = new();
    
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
        
        _referenceMap.Clear();

        using var writer = new BinaryWriter(dataStream);
        
        // write the first header
        WriteSection(SectionNumbers.Header, writer);
        
        // write the cache section
        WriteSection(SectionNumbers.Cache, writer);
        
        // write the third data section
        WriteSection(SectionNumbers.Data, writer);
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
        
        _referenceMap.Clear();
        
        using var writer = new BinaryWriter(dataStream);
        
        // write the first header
        WriteSection(SectionNumbers.Header, writer);
        
        // write the cache section
        WriteSection(SectionNumbers.Cache, writer);
        
        // write the third data section
        WriteSection(SectionNumbers.Data, writer);
    }
    #endregion
    
    #region section write

    /// <summary>
    /// Used for writing desired sections.
    /// </summary>
    /// <param name="sectionNumber">The section to write</param>
    /// <param name="writer">The binary writer param</param>
    /// <exception cref="ArgumentOutOfRangeException">Called if there is no such ClassSection or it is not implemented.</exception>
    private void WriteSection(SectionNumbers sectionNumber, BinaryWriter writer)
    {
        // already written the byte code here.
        writer.Write((byte)Markers.StartSection);
        writer.Write((int)sectionNumber);

        switch (sectionNumber)
        {
            case SectionNumbers.Header:
                WriteSectionHeader(writer);
                break;
            case SectionNumbers.Cache:
                WriteSectionCache(writer);
                break;
            case SectionNumbers.Data:
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
        var cacheSection = new CacheSection()
        {
            CacheMode = (int)_cacheMode
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
        var fields = classType.GetFields(Manager.BindingFlagsAll);
            
        // process fields
        foreach (var field in fields)
        {
            WriteFieldNoCache(field, classData, writer);
        }
        
        // get properties
        var properties= classType.GetProperties(Manager.BindingFlagsAll);
        
        // process properties
        foreach (var property in properties)
        {
            WritePropertyNoCache(property, classData, writer);
        }
        
        writer.Write((byte)Markers.EndScope);
    }

    #region write fields and properties
    private void WriteFieldNoCache(FieldInfo field, object baseClass, BinaryWriter writer)
    {
        // check if field can be serialized
        if (field.IsPublic && field.GetCustomAttribute<DoNotSerialize>(false) != null) return;
        if (field.IsPrivate && field.GetCustomAttribute<ForceSerialize>(false) == null) return;

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
        var isPublic = property is { CanRead: true, CanWrite: true };

        if (isPublic && property.GetCustomAttribute<DoNotSerialize>(false) != null) return;
        if (!isPublic && property.GetCustomAttribute<ForceSerialize>(false) == null) return;
        
        var propertyVal = property.GetValue(baseClass);
        if (propertyVal == null) return;
        
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
        WritePrimitiveDataNoCache(data, writer);
        writer.Write((byte)Markers.EndScope);
    }
    
    #region write primitive data
    /// <summary>
    /// Writes the primitive data type value (excluding ISerializable because it got its own function).
    /// </summary>
    /// <param name="data">The data to write</param>
    /// <param name="writer">The binary writer</param>
    /// <exception cref="Exception">Primitive data type not implemented yet.</exception>
    private void WritePrimitiveDataNoCache(object data, BinaryWriter writer)
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
    
    #endregion
    
    #region collection writes
    private void WriteCollectionNoCache(Type dataType, ICollection collection, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartCollection);
        
        var collectionCount = collection.Count;
        
        writer.Write(dataType.AssemblyQualifiedName);
        
        // write all the params to this collection
        var genericArguments = dataType.GetGenericArguments();
        writer.Write(genericArguments.Length);
        
        foreach (var argType in genericArguments)
        {
            writer.Write(argType.AssemblyQualifiedName);
        }
        
        if (!Manager.GetCollectionInterfaceByteCode(dataType, out var collectionByteCode))
            throw new($"Can't find collection byte code for {dataType.Name}");
        
        writer.Write(collectionByteCode);
        writer.Write(collectionCount);
        
        switch (collectionByteCode)
        {
            case (byte)CollectionInterfaces.IList:
                WriteIListNoCache(collection as IList, genericArguments, writer);
                break;
            case (byte)CollectionInterfaces.IDictionary:
                WriteIDictionaryNoCache(collection as IDictionary, genericArguments, writer);
                break;
            default:
                throw new("Unsupported parsing ICollection type " + dataType.Name);
        }

        writer.Write((byte)Markers.EndScope);
    }
    
    #region write ilist

    private void WriteIListNoCache(IList? data, Type[] genericArgs, BinaryWriter writer)
    {
        if (data == null) return;
        
        if (genericArgs.Length != 1)
            throw new($"Passed in generic arguments size is not '1'.");
        
        // serialize each element
        foreach (var item in data)
        {
            writer.Write((byte)CollectionMarkers.IListElementStart);
            SerializeNoCache(item, genericArgs[0], writer);
            writer.Write((byte)Markers.EndScope);
        }
    }
    #endregion
    
    #region write IDictionary
    private void WriteIDictionaryNoCache(IDictionary? data, Type[] genericArgs, BinaryWriter writer)
    {
        if (data == null) return;
        // generic args array MUST be 2
        if (genericArgs.Length != 2)
            throw new($"Passed in generic arguments size is not '2'.");
        
        foreach (DictionaryEntry item in data)
        {
            // write key
            writer.Write((byte)CollectionMarkers.IDictionaryKeyStart);
            SerializeNoCache(item.Key, genericArgs[0], writer);
            writer.Write((byte)Markers.EndScope);
            
            // write value
            writer.Write((byte)CollectionMarkers.IDictionaryValueStart);
            SerializeNoCache(item.Value, genericArgs[1], writer);
            writer.Write((byte)Markers.EndScope);
        }
    }
    #endregion
    
    #endregion
    
    #endregion
    
    #region General Functions
    void Serialize(object desiredObj, Type desiredType, BinaryWriter writer)
    {
        switch (_cacheMode)
        {
            case CacheMode.None: SerializeNoCache(desiredObj, desiredType, writer); break;
            default: throw new ArgumentOutOfRangeException();
        }
    }
    
    #endregion
}