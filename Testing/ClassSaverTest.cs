using System;
using ClassSaver.Structure;

namespace ClassSaver.Testing;

/// <summary>
/// Used for testing purposes
/// </summary>
static class ClassSaverTest
{
    
    private static class RandomString
    {
        private static Random _random = new Random();

        public static string GetRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }
    }
    
    private abstract class TestClass
    {
        public abstract override string ToString();
        public abstract void RandomData();
        public bool IsCorrect(object toCheck)
        {
            return false;
        }
    }
    
    private class Vector3 : TestClass
    {
        public int x;
        public int y;
        public int z;
        
        // there is a chance of infinite recursion if we self-reference.
        // TODO: FIX IT
        

        public override string ToString()
        {
            return $"{x}, {y}, {z}";
        }

        public override void RandomData()
        {
            Random rnd = new Random();
            const int minVal = -999;
            const int maxVal = 1000;
            
            x = rnd.Next(minVal, maxVal);
            y = rnd.Next(minVal, maxVal);
            z = rnd.Next(minVal, maxVal);
        }
    }

    private class Classroom : TestClass
    {
        public string classCode;
        public Vector3 position;
        
        [ForceSerialize]
        private string className = "67GC";
        
        [ForceSerialize]
        private Dictionary<string, int> classes = new();

        public override string ToString()
        {
            var partOne = $"{classCode} | {className} | {position} | ";

            string partTwo = "";

            foreach (var kvp in classes)
            {
                partTwo += $"{kvp}"; // dict does not remain ordering though.
            }
            
            return partOne + partTwo;
        }

        public override void RandomData()
        {
            Random rnd = new Random();
            position = new();
            position.RandomData();
            
            classCode = RandomString.GetRandomString(10);
            className = RandomString.GetRandomString(3);
            
            int dictLength = rnd.Next(1, 30);

            for (int i = 0; i < dictLength; i++)
            {
                var toAdd = (RandomString.GetRandomString(dictLength), rnd.Next(1, 20));
                while (classes.ContainsKey(toAdd.Item1))
                {
                    toAdd = (RandomString.GetRandomString(dictLength), rnd.Next(1, 20));
                }
                
                classes.Add(toAdd.Item1, toAdd.Item2);
            }
        }
    }

    private struct Vector2
    {
        public int x;
        public int y;
        
        public override string ToString()
        {
            return $"{x}, {y}";
        }
    }

    private class SelfRefTest : TestClass
    {
        public SelfRefTest _referenceSelf;

        public int foo = 0;
        public string boo = "8fj2jf3f2";

        public override void RandomData()
        {
            Random rnd = new Random();

            foo = rnd.Next(1, 10000);
            boo = RandomString.GetRandomString(10);

            _referenceSelf = this;
        }

        public override string ToString()
        {
            return $"{_referenceSelf.foo} , {_referenceSelf.boo}, {foo}, {boo}";
        }
    }


    private class NullTest : TestClass
    {
        public Vector2? vector2 = null;

        public override void RandomData()
        {
            
        }

        public override string ToString()
        {
            if (vector2 == null) return "pass";

            return "fail";
        }
    }


    private static readonly Type[] testableTypes =
    [
        typeof(Vector3),
        typeof(Classroom),
        typeof(Vector2),
        typeof(SelfRefTest),
        typeof(NullTest)
    ];

    private const string fileExtension = "test";
    private static Random _random;

    private static bool Test(int testNum, ClassSerializer serializer, ClassParser parser)
    {
        int typeIndex = _random.Next(testableTypes.Length);
        Type currentType = testableTypes[typeIndex];
        
        if (currentType.IsClass) return TestClassType(currentType, testNum, serializer, parser);
        
        return TestStructType(currentType, testNum, serializer, parser);
    }
    
    private static bool TestClassType(Type currentType, int testNum, ClassSerializer serializer, ClassParser parser)
    {
        var objectA = Activator.CreateInstance(currentType) as TestClass;
        object objectB;
        objectA?.RandomData();
    
        string fileName = $"{currentType.Name}_{testNum}.{fileExtension}";
        
        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
        {
            serializer.Serialize(objectA, fileStream);
        }
        
        // 50 50 chance it creates new object or reference.
        int chance = _random.Next(0, 2);
        using (var readStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))

        if (chance == 0)
        {
            objectB = parser.Parse(readStream);
        }
        else
        {
            objectB = Activator.CreateInstance(currentType);
            parser.ParseTo(objectB, readStream);
        }

        if (objectA.ToString() == objectB.ToString())
        {
            Console.WriteLine($"Test {testNum} passed.");
        
            // Delete the file only if the test passes
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            return true;
        }

        Console.WriteLine($"Test {testNum} failed.");
        Console.WriteLine($"{objectA} vs {objectB}");
        return false;
    }

    private static bool TestStructType(Type currentType, int testNum, ClassSerializer serializer, ClassParser parser)
    {
        var objectA = Activator.CreateInstance(currentType);
        object? objectB;
        
        string fileName = $"{currentType.Name}_{testNum}.{fileExtension}";
        
        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
        {
            serializer.Serialize(objectA, fileStream, CacheMode.Keyword);
        }
        
        using (var readStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
            objectB = parser.Parse(readStream);
        }

        if (objectA.ToString() == objectB.ToString())
        {
            Console.WriteLine($"Test {testNum} passed.");
        
            // Delete the file only if the test passes
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            return true;
        }
        
        Console.WriteLine($"Test {testNum} failed.");
        Console.WriteLine($"{objectA} vs {objectB}");
        return false;
    }

    public static void Main()
    {
        Console.Out.Write("Enter the number of tests to run: ");
        int numOfTests = int.Parse(Console.In.ReadLine());
        
        var serializer = new ClassSerializer();
        var parser = new ClassParser();
        _random = new Random();

        for (int testNumber = 1; testNumber <= numOfTests; testNumber++)
        {
            var res = Test(testNumber, serializer, parser);
            if (!res) throw new("Test failed.");
        }
    }
}