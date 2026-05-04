using System;

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
                partTwo += $"{kvp}";
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
                classes.Add(RandomString.GetRandomString(dictLength), rnd.Next(1, 100));
            }
        }
    }
    
    
    private static List<Type> testableTypes = new()
    {
        typeof(Vector3),
        typeof(Classroom),
    };

    private static string fileExtension = "test";

    private static bool Test(Random random, int testNum, ClassSerializer serializer, ClassParser parser)
    {
        int typeIndex = random.Next(testableTypes.Count);
        Type currentType = testableTypes[typeIndex];
    
        var objectA = Activator.CreateInstance(currentType) as TestClass;
        object objectB;
        objectA?.RandomData();
    
        string fileName = $"{currentType.Name}_{testNum}.{fileExtension}";

        
        using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
        {
            serializer.Serialize(objectA, fileStream);
        }
        
        using (var readStream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
        {
            objectB = parser.Parse(currentType, readStream);
        }

        if (objectA?.ToString() == objectB?.ToString())
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
        var random = new Random();

        for (int testNumber = 1; testNumber <= numOfTests; testNumber++)
        {
            bool res = Test(random, testNumber, serializer, parser);
            if (!res) throw new("Test failed.");
        }
    }
}