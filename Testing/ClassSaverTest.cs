using System;

namespace ClassSaver.Testing;

/// <summary>
/// Used for testing purposes
/// </summary>
public class ClassSaverTest
{
    public class DummyClass
    {
        public int x = 3;
        [DoNotSerialize]
        public int y = 6;
        [ForceSerialize]
        private int z = 333;
    }

    public static void Main()
    {
        TextWriter output = Console.Out;
        output.WriteLine("Start inserting dummy class...");

        var savePath = "dummyClass.bin";
        using var saveFile = new FileStream(savePath, FileMode.Create);
        
        var dummyObj = new DummyClass();
        ClassSerializer serializer = new ClassSerializer();
        serializer.Serialize<DummyClass>(dummyObj, saveFile, CacheMode.None);
        
        output.WriteLine("Done!");
    }
}