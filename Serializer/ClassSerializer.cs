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

        using var writer = new BinaryWriter(dataStream);
        
        // write the first header
        WriteSection(ClassSection.Header, writer);
        
        // write the cache section
        WriteSection(ClassSection.Cache, writer);
        
        // write the third data section
        WriteSection(ClassSection.Data, writer);
    }
    
    public void Serialize(object desiredObj, Stream dataStream, CacheMode cacheMode = CacheMode.None)
    {
        _cacheMode = cacheMode;
        _currentSerializingType = desiredObj.GetType();
        _currentSerializingObj = desiredObj;
        
        using var writer = new BinaryWriter(dataStream);
        
        // write the first header
        WriteSection(ClassSection.Header, writer);
        
        // write the cache section
        WriteSection(ClassSection.Cache, writer);
        
        // write the third data section
        WriteSection(ClassSection.Data, writer);
    }
    #endregion
    
    #region section write

    /// <summary>
    /// Used for writing desired sections.
    /// </summary>
    /// <param name="section">The section to write</param>
    /// <param name="writer">The binary writer param</param>
    /// <exception cref="ArgumentOutOfRangeException">Called if there is no such ClassSection or it is not implemented.</exception>
    private void WriteSection(ClassSection section, BinaryWriter writer)
    {
        // already written the byte code here.
        writer.Write((byte)Markers.StartSection);
        writer.Write((int)section);

        switch (section)
        {
            case ClassSection.Header:
                WriteSectionHeader(writer);
                break;
            case ClassSection.Cache:
                WriteSectionCache(writer);
                break;
            case ClassSection.Data:
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
    private void SerializeNoCache(object? desiredObj, Type objectType, BinaryWriter writer, string objectVarName = "")
    {
        if (string.IsNullOrEmpty(objectVarName)) objectVarName = objectType.Name;
        
        // is a primitive data type
        if (Manager.IsPrimitive(objectType))
        {
            WritePrimitiveNoCache(objectVarName, desiredObj, writer);
            return;
        }
        
        // is Collection
        if (desiredObj is ICollection Collection)
        {
            WriteCollectionNoCache(objectVarName, desiredObj.GetType(), Collection, writer);
            return;
        }
        
        // is a class
        WriteClassNoCache(objectVarName, desiredObj, writer);
    }
    #endregion

    #region class writer
    private void WriteClassNoCache(string varName, object classData, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartClass);
        writer.Write(varName);
        
        var classType = classData.GetType();
        writer.Write(classType.AssemblyQualifiedName);

        // get fields & save
        var fields = classType.GetFields(Manager.BindingFlagsAll);
            
        // process fields
        foreach (var field in fields)
        {
            // check if field can be serialized
            if (field.IsPublic && field.GetCustomAttribute<DoNotSerialize>(false) != null) continue;
            if (field.IsPrivate && field.GetCustomAttribute<ForceSerialize>(false) == null) continue;
            
            SerializeNoCache(field.GetValue(classData), field.FieldType, writer, field.Name);
        }
        
        // process properties
        var properties =  classType.GetProperties(Manager.BindingFlagsAll);
        
        // process properties
        foreach (var property in properties)
        {
            // check if property can be serialized
            var isPublic = property is { CanRead: true, CanWrite: true };

            if (isPublic && property.GetCustomAttribute<DoNotSerialize>(false) != null) continue;
            if (!isPublic && property.GetCustomAttribute<ForceSerialize>(false) == null) continue;
            
            SerializeNoCache(property.GetValue(classData), property.PropertyType, writer, property.Name);
        }
        
        writer.Write((byte)Markers.EndScope);
    }
    #endregion
    
    #region ISerializable write
    /// <summary>
    /// Writes the object that implements the ISerializable interface.
    /// </summary>
    /// <param name="varName">The variable's name</param>
    /// <param name="baseType">The base type of the serializable</param>
    /// <param name="value">The object value that implements ISerializable</param>
    /// <param name="writer">The binary writer</param>
    private void WriteSerializableNoCache(string varName, Type baseType, ISerializable value, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartSerializable);
        writer.Write(varName);
        writer.Write(baseType.AssemblyQualifiedName);
        
        var toSerialize = value.Serialize();
        SerializeNoCache(toSerialize, toSerialize.GetType(), writer);
        
        writer.Write((byte)Markers.EndScope);
    }
    #endregion
    
    #region Write primitive
    private void WritePrimitiveNoCache(string varName, object? data, BinaryWriter writer)
    {
        var dataType = data?.GetType();
        
        // check if serializable or not to write
        if (Manager.IsPrimitiveDatatypeOf(dataType, PrimitiveDatatypes.ISerializable))
        {
            WriteSerializableNoCache(varName, dataType, data as ISerializable, writer);
            return;
        }
        
        // mark as variable
        writer.Write((byte)Markers.StartVariable);
        // write var name
        writer.Write(varName);
        
        // write the data
        WritePrimitiveDataNoCache(data, writer);
        writer.Write((byte)Markers.EndScope);
    }
    
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
    
    #region collection writes
    private void WriteCollectionNoCache(string varName, Type dataType, ICollection collection, BinaryWriter writer)
    {
        writer.Write((byte)Markers.StartCollection);
        writer.Write(varName);
        
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