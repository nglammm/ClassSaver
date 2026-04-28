namespace ClassSaver;

/// <summary>
/// An interface for classes that is serializeable and parseable back and forth so the serializer/parser
/// won't dig deep into each variable.
/// <para>
/// Please make sure the returning object can be converted back
/// </para>
/// </summary>
public interface ISerializable
{
    /// <summary>
    /// Called upon serializing.
    /// </summary>
    /// <returns>Object after serialize, must be convertable back upon calling Parse(obj).</returns>
    public object Serialize();
    
    /// <summary>
    /// Called upon parsing.
    /// </summary>
    /// <param name="obj">The object to parse back.</param>
    public void Parse(object obj);
}