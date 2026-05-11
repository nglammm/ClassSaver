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
    Boolean = 10,
    Byte = 11,
    SByte = 12,

    // 2 Bytes (16-bit)
    Char = 13,
    Int16 = 14,
    UInt16 = 15,

    // 4 Bytes (32-bit)
    Int32 = 16,
    UInt32 = 17,
    Single = 18, // float

    // 8 Bytes (64-bit)
    Int64 = 19,
    UInt64 = 20,
    Double = 21,

    // 16 Bytes (128-bit)
    Decimal = 22,

    // Reference Types
    String = 23,
    Null = 24,
    
    // Custom types
    ISerializable = 25
}