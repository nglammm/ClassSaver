using System.Collections;
using System.Reflection;

namespace ClassSaver;

/// <summary>
/// Serializes a class from a given stream.
/// </summary>
public class ClassSerializer
{
    private CacheMode _cacheMode;
    private Type? _currentSerializingType;
    private object? _currentSerializingObj;

    /// <summary>
    /// Serializes the desired object and then tries to save it.
    /// </summary>
    /// <param name="desiredObj">The desired object to parse</param>
    /// <param name="dataStream">The file stream to output with</param>
    /// <param name="cacheMode">The cache mode to serialize with</param>
    public void Serialize<T>(object desiredObj, Stream dataStream, CacheMode cacheMode = CacheMode.None) where T : new()
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

    /// <summary>
    /// Used for writing desired sections.
    /// </summary>
    /// <param name="section">The section to write</param>
    /// <param name="writer">The binary writer param</param>
    /// <exception cref="ArgumentOutOfRangeException">Called if there is no such ClassSection or it is not implemented.</exception>
    private void WriteSection(ClassSection section, BinaryWriter writer)
    {
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartSection"));
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

        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }

    
    private void WriteSectionHeader(BinaryWriter writer)
    {
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartSection"));
        writer.Write((int)ClassSection.Header);
        
        // [TODO] make header section
        var headerSection = new HeaderSection();
        Serialize(headerSection, headerSection.GetType(), writer);
        
        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }

    private void WriteSectionCache(BinaryWriter writer)
    {
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartSection"));
        writer.Write((int)ClassSection.Cache);
        
        // [TODO] make cache section
        var cacheSection = new CacheSection()
        {
            CacheMode = (int)_cacheMode
        };
        
        Serialize(cacheSection, cacheSection.GetType(), writer);
        
        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }

    private void WriteSectionData(BinaryWriter writer)
    {
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartSection"));
        writer.Write((int)ClassSection.Data);
        
        Serialize(_currentSerializingObj, _currentSerializingType, writer);
        
        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }


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
    private void Serialize(object desiredObj, Type objectType, BinaryWriter writer, string objectVarName = "")
    {
        if (string.IsNullOrEmpty(objectVarName)) objectVarName = objectType.Name;
        
        // is primitive
        if (ClassSaverManager.IsPrimitive(objectType))
        {
            // check for primitive type
            if (objectType.Name == "ISerializable") WriteSerializable(objectVarName, desiredObj as ISerializable, writer);
            else WritePrimitive(objectVarName, desiredObj, writer);
            return; // quit early
        }
        
        // is Collection
        if (desiredObj is ICollection Collection)
        {
            WriteCollection(objectVarName, desiredObj.GetType(), Collection, writer);
            return;
        }
        
        // is a class
        WriteClass(objectVarName, desiredObj, writer);
    }

    private void WriteClass(string varName, object classData, BinaryWriter writer)
    {
        writer.Write(varName);
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartClass"));
        
        var classType = classData.GetType();
        writer.Write(classType.AssemblyQualifiedName);

        // get fields & save
        var fields = classType.GetFields(ClassSaverManager.BindingFlagsAll);
            
        // process fields
        foreach (var field in fields)
        {
            // check if field can be serialized
            if (field.IsPublic && field.GetCustomAttribute<DoNotSerialize>(false) != null) continue;
            if (field.IsPrivate && field.GetCustomAttribute<ForceSerialize>(false) == null) continue;

            Serialize(field.GetValue(classData), field.FieldType, writer, field.Name);
        }
        
        // process properties
        var properties =  classType.GetProperties(ClassSaverManager.BindingFlagsAll);
        
        // process properties
        foreach (var property in properties)
        {
            // check if property can be serialized
            bool isPublic = property.CanRead && property.CanWrite;

            if (isPublic && property.GetCustomAttribute<DoNotSerialize>(false) != null) continue;
            if (!isPublic && property.GetCustomAttribute<ForceSerialize>(false) == null) continue;
            
            Serialize(property.GetValue(classData), property.PropertyType, writer, property.Name);
        }

        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }
    
    /// <summary>
    /// Writes the object that implements the ISerializable interface.
    /// </summary>
    /// <param name="varName">The variable's name</param>
    /// <param name="value">The object value that implements ISerializable</param>
    /// <param name="writer">The binary writer</param>
    private void WriteSerializable(string varName, ISerializable value, BinaryWriter writer)
    {
        writer.Write(varName);
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartSerializable"));
        
        var toSerialize = value.Serialize();
        Serialize(toSerialize, toSerialize.GetType(), writer);
        
        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }

    /// <summary>
    /// Writes the primitive type.
    /// </summary>
    /// <param name="varName">It's variable name.</param>
    /// <param name="value">The value to save to.</param>
    /// <param name="writer">The writer instance</param>
    private void WritePrimitive(string varName, object value, BinaryWriter writer)
    {
        // useful for future cases
        switch (_cacheMode)
        {
            case CacheMode.None: WritePrimitiveNoCache(varName, value, writer); break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void WritePrimitiveNoCache(string varName, object data, BinaryWriter writer)
    {
        // write var name
        writer.Write(varName);
        // mark as variable
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartVariable"));
        
        // write data type code
        if (ClassSaverManager.GetPrimitiveByteCode(data, out var varTypeCode)) writer.Write(varTypeCode);
        else throw new($"Unsupported primitive data type '{data.GetType().Name}'.");
        
        // write the data
        WritePrimitiveData(data, writer);
        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }

    private static void WritePrimitiveData(object data, BinaryWriter writer)
    {
        var dataType = data.GetType();

        switch (Type.GetTypeCode(dataType))
        {
            case TypeCode.Boolean:
                writer.Write((bool)data);
                break;
            case TypeCode.Byte:
                writer.Write((byte)data);
                break;
            case TypeCode.SByte:
                writer.Write((sbyte)data);
                break;
            case TypeCode.Char:
                writer.Write((char)data);
                break;
            case TypeCode.Decimal:
                writer.Write((decimal)data);
                break;
            case TypeCode.Double:
                writer.Write((double)data);
                break;
            case TypeCode.Single:
                writer.Write((float)data);
                break;
            case TypeCode.Int32:
                writer.Write((int)data);
                break;
            case TypeCode.UInt32:
                writer.Write((uint)data);
                break;
            case TypeCode.Int64:
                writer.Write((long)data);
                break;
            case TypeCode.UInt64:
                writer.Write((ulong)data);
                break;
            case TypeCode.Int16:
                writer.Write((short)data);
                break;
            case TypeCode.UInt16:
                writer.Write((ushort)data);
                break;
            case TypeCode.String:
                // binary writer already includes the length of string
                writer.Write((string)data);
                break;
            case TypeCode.Empty:
                // already wrote the null byte.
                break;
            default:
                throw new($"Primitive data type '{data.GetType().Name}' is not implemented.");
        }
    }

    private void WriteCollection(string varName, Type dataType, ICollection collection, BinaryWriter writer)
    {
        writer.Write(varName);
        writer.Write(ClassSaverManager.GetMarkerByteCode("StartCollection"));
        
        // get the Collection count
        var objects = collection as object[] ?? collection.Cast<object>().ToArray();
        var collectionCount = collection.Count;
        
        writer.Write(dataType.Name);
        writer.Write(collectionCount);
        
        // serialize each element
        foreach (var obj in objects)
        {
            var objType = obj.GetType();
            Serialize(obj, objType, writer);
        }

        writer.Write(ClassSaverManager.GetMarkerByteCode("EndScope"));
    }
}