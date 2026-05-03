using System;

namespace ClassSaver.Testing;

/// <summary>
/// Used for testing purposes
/// </summary>
public class ClassSaverTest
{
    private class DummyClass1 : IEquatable<DummyClass1>
    {
        public int x { get; set; } // property test
        
        [ForceSerialize]
        private int y = 9995; // field test
        
        [ForceSerialize]
        public int z { get; private set; } // property-readonly
        
        // there is a chance of infinite recursion if we self-reference.
        // TODO: FIX IT
        
        public bool Equals(DummyClass1? other)
        {
            return x == other.x && y == 9995 && z == other.z;
        }

        public override string ToString()
        {
            return $"{x}, {y}, {z}";
        }
    }
    
    

    public static void Main()
    {
        TextWriter output = Console.Out;
        output.WriteLine("Start Serializing ---------");

        var savePath = "dummyClass.bin";
        using var saveFile = new FileStream(savePath, FileMode.Create);
        
        var dummyObj = new DummyClass1();
        dummyObj.x = 123;
        //dummyObj.y = 123;
        
        ClassSerializer serializer = new ClassSerializer();
        serializer.Serialize<DummyClass1>(dummyObj, saveFile, CacheMode.None);
        
        output.WriteLine("Done Serializing! ---------");
        output.WriteLine("");
        
        output.WriteLine("Start Parsing ---------");
        using var parsingFile = new FileStream(savePath, FileMode.Open);
        
        ClassParser parser = new ClassParser();
        var parsedDummyObj = parser.Parse<DummyClass1>(parsingFile);
        
        output.WriteLine("Done Parsing ---------");
        output.WriteLine("");
        
        output.WriteLine("Input: ");
        output.WriteLine(dummyObj.ToString());

        output.WriteLine("Output: ");
        output.WriteLine(parsedDummyObj.ToString());
    }
}