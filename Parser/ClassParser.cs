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
    private HeaderSection? _headerSection;
    private CacheSection? _cacheSection;
    
    public T Parse<T>(Stream stream) where T : new()
    {
        using var reader = new BinaryReader(stream);
        
        _headerSection = ReadSection<HeaderSection>(ClassSection.Header, reader);
        _cacheSection = ReadSection<CacheSection>(ClassSection.Cache, reader);
        
        return ReadSection<T>(ClassSection.Data, reader);
    }
    
    /// <summary>
    /// Gets the header section from the latest class to parse.
    /// </summary>
    /// <returns>The header section</returns>
    public HeaderSection GetHeaderSection() => _headerSection;
    
    /// <summary>
    /// Gets the cache section from the latest class to parse.
    /// </summary>
    /// <returns>The cache section</returns>
    public CacheSection GetCacheSection() => _cacheSection;

    private T ReadSection<T>(ClassSection section, BinaryReader reader) where T : new()
    {
        var startSectionByte = reader.ReadByte();
        if (ClassSaverManager.GetPrimitiveByteCode("StartSection", out var expectedStartByte) && expectedStartByte != startSectionByte)
        {
            throw new($"Expected '{expectedStartByte:x}', got '{startSectionByte:x}'.");
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
                return !IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope"), false) ? throw new("The expected end byte does not match") : ReadSectionData<T>(reader);
            default:
                throw new Exception($"Unimplemented section type {section}");
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
        throw new NotImplementedException();
    }

    private CacheSection ReadSectionCache(BinaryReader reader)
    {
        throw new NotImplementedException();
    }

    private T ReadSectionData<T>(BinaryReader reader) where T : new()
    {
        throw new NotImplementedException();
    }

    private T ReadClass<T>(BinaryReader reader) where T : new()
    {
        return (T)ReadClass(typeof(T), reader);
    }

    private object ReadClass(Type tType, BinaryReader reader)
    {
        // start is the class byte identifier
        var classByte = reader.ReadByte();
        
        // edge cases
        if (classByte != ClassSaverManager.GetMarkerByteCode("StartClass"))
            throw new($"Trying to parse class '{tType.Name}' but " +
                      "there is no start class byte.");

        var className = reader.ReadString();
        if (className != tType.Name) // class name request vs data mismatch
            throw new($"Requested to parse class '{tType.Name}' but data has class '{className}'.");
        
        // preload all variables
        var fieldsMap = tType.GetFields(ClassSaverManager.BindingFlagsAll).ToDictionary(field => field.Name);
        var propertiesMap = tType.GetProperties(ClassSaverManager.BindingFlagsAll).ToDictionary(p => p.Name);
        
        // preparing the outputs
        var outputClass = Activator.CreateInstance(tType);
        var scope = 1;

        while (scope >= 1)
        {
            if (IsByte(reader, ClassSaverManager.GetMarkerByteCode("EndScope"), false)) scope--;
            else scope++;
            
            // var name
            var varName = reader.ReadString();
            var varTypeByte = reader.ReadByte();
            object varItem;
            
            // map to the correct section
            if (varTypeByte == ClassSaverManager.GetMarkerByteCode("StartCollection")) varItem = ReadCollection(reader);
            if (varTypeByte == ClassSaverManager.GetMarkerByteCode("StartClass")) varItem = ReadClass(Type.GetType(reader.ReadString()), reader); // name is qualified
            
            
        }
        
        throw new Exception($"Class '{tType.Name}' hasn't been parsed yet.");
    }

    private ICollection ReadCollection(BinaryReader reader)
    {
        throw new NotImplementedException();
    }
    
    
}