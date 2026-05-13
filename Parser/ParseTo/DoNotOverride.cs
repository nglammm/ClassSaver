namespace ClassSaver.Parser.ParseTo;

/// <summary>
/// Attribute used to not override a variable when using the ParseTo() function on ClassParser
/// if fits a certain criteria (optional).
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public class DoNotOverride : Attribute
{
    public string FuncCheckName {get; private set;}
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="funcCheckName">
    /// The function name that returns a boolean with a parameter of object to determine
    /// if we should override this variable or not.
    /// </param>
    public DoNotOverride(string funcCheckName = default)
    {
        FuncCheckName = funcCheckName;
    }
}