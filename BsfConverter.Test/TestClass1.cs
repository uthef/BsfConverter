namespace BsfConverter.Test;

public class TestClass1
{
    public TestClass2[] SubClasses;
    public bool Boolean { get; }


    public TestClass1(TestClass2[] subClasses, bool boolean)
    {
        SubClasses = subClasses;
        Boolean = boolean;
    }

    public static bool AreEquivalent(TestClass1 a, TestClass1 b)
    {
        if (a.SubClasses.Length != b.SubClasses.Length || a.Boolean != b.Boolean) return false;

        int i = 0;

        foreach (var aSubClass in a.SubClasses)
        {
            if (!TestClass2.AreEquivalent(aSubClass, b.SubClasses[i++])) return false;
        }

        return true;
    }
}
