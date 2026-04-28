using System.Collections;
using System.Reflection;

namespace ClassSaver;

/// <summary>
/// Serializes a class and saves to a file.
/// </summary>
public class ClassSerializer
{
    private CacheMode _cacheMode;
    private ClassSection _currentSection;
    private Type? _currentSerializingType;
    private object? _currentSerializingObj;

    /// <summary>
    /// Serializes the desired object and then tries to save it.
    /// </summary>
    /// <param name="desiredObj">The desired object to parse</param>
    /// <param name="dataStream">The file stream to output with</param>
    /// <param name="cacheMode">The cache mode to serialize with</param>
    public void Serialize<T>(object desiredObj, Stream dataStream, CacheMode cacheMode = CacheMode.None)
    {
        _cacheMode = cacheMode;
        _currentSerializingType = typeof(T);
        _currentSerializingObj = desiredObj;

        using var writer = new BinaryWriter(dataStream);

        WriteSection(ClassSection.Header, writer);
        _currentSection++;
        WriteSection(ClassSection.Data, writer);
    }


    private void WriteSection(ClassSection section, BinaryWriter writer)
    {
        writer.Write(ClassSaverManager.StartSection);
        writer.Write((int)section);

        switch (section)
        {
            case ClassSection.Header:
                // writes nothing for now
                break;
            case ClassSection.Data:
                // data section
                Serialize(_currentSerializingObj, _currentSerializingType, writer);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        writer.Write(ClassSaverManager.EndScope);
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
        while (true)
        {
            if (string.IsNullOrEmpty(objectVarName)) objectVarName = objectType.Name;

            writer.Write(ClassSaverManager.StartClass);
            writer.Write(objectVarName);

            // is primitive
            if (ClassSaverManager.IsPrimitive(objectType))
            {
                WritePrimitive(objectVarName, desiredObj, writer);
                return;
            }

            switch (desiredObj)
            {
                case IEnumerable:
                    WriteEnumerable(objectVarName, desiredObj, writer);
                    return;
                // implements ISerializable
                case ISerializable serializable:
                {
                    var serializeObj = serializable.Serialize();
                    desiredObj = serializeObj;
                    objectType = serializeObj.GetType();
                    continue;
                }
            }

            // get fields & save
            var fields = objectType.GetFields(ClassSaverManager.BindingFlagsAll);

            foreach (var field in fields)
            {
                // check if field can be serialized
                if (field.IsPublic && field.GetCustomAttribute<DoNotSerialize>(false) != null) continue;
                if (field.IsPrivate && field.GetCustomAttribute<ForceSerialize>(false) == null) continue;

                Serialize(field.GetValue(desiredObj), field.FieldType, writer, field.Name);
            }

            writer.Write(ClassSaverManager.EndScope);
            break;
        }
    }

    /// <summary>
    /// Writes the primitive type.
    /// </summary>
    /// <param name="varName">It's variable name.</param>
    /// <param name="value">The value to save to.</param>
    /// <param name="writer">The writer instance</param>
    private void WritePrimitive(string varName, object value, BinaryWriter writer)
    {
        // uncached so we write var name
        switch (_cacheMode)
        {
            case CacheMode.None: WritePrimitiveNoCache(varName, value, writer); break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void WritePrimitiveNoCache(string varName, object data, BinaryWriter writer)
    {
        // write var name
        writer.Write(varName);
        
        // write data type code
        if (ClassSaverManager.GetPrimitiveByteCode(data, out var varTypeCode)) writer.Write(varTypeCode);
        else throw new($"Unsupported primitive data type '{data.GetType().Name}'.");
        
        // write the data
        WritePrimitiveData(data, writer);
    }

    private void WritePrimitiveData(object data, BinaryWriter writer)
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

    private void WriteEnumerable(string varName, object data, BinaryWriter writer)
    {
        writer.Write(ClassSaverManager.StartEnumerable);
        writer.Write(varName);

        IEnumerable? enumerable = data as IEnumerable;
        if (enumerable == null) throw new($"'{data.GetType().Name}' is not an enumerable.");

        var enumerableCount = data switch {
            ICollection c => c.Count,
            IEnumerable e => e.Cast<object>().Count(),
            _ => 0
        };
        
        writer.Write(enumerableCount);
        
        foreach (object obj in enumerable)
        {
            Type objType = obj.GetType();
            Serialize(obj, objType, writer);
        }

        writer.Write(ClassSaverManager.EndScope);
    }
}