namespace ClassSaver.Constants;

/// <summary>
/// All the ClassSaver's primitive data types that ClassSaver supports.
/// </summary>
public enum PrimitiveDatatypes
{
    /// internal note ----
    /// Make sure the enum element the type name exactly and is case-sensitive.
    /// Also make sure the equivalent integer value is unique and does not exist
    /// in any enum in constants. 
    /// -----------------
    
    // 1 Byte (8-bit)
    Boolean = 16,
    Byte = 17,
    SByte = 18,

    // 2 Bytes (16-bit)
    Char = 19,
    Int16 = 20,
    UInt16 = 21,

    // 4 Bytes (32-bit)
    Int32 = 22,
    UInt32 = 23,
    Single = 24, // float

    // 8 Bytes (64-bit)
    Int64 = 25,
    UInt64 = 26,
    Double = 27,

    // 16 Bytes (128-bit)
    Decimal = 28,

    // Reference Types
    String = 29,
    Null = 30,
    
    // Custom types
    ISerializable = 31
}