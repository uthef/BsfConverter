namespace BsfConverter.Test;

public class TestClass2
{
    public Guid Id { get; } = Guid.NewGuid();
    public double Number { get; } = 23.1;
    public TestEnum Enum { get; }

    [Core.Attributes.BsfConstructor]    
    public TestClass2(byte[] id, double number, TestEnum @enum)
    {
        Enum = @enum;
        Number = number;
        Id = new Guid(id);
    }

    public TestClass2(Guid guid, double number, TestEnum @enum)
    {
        Enum = @enum;
        Number = number;
        Id = guid;
    }
    public static bool AreEquivalent(TestClass2 a, TestClass2 b)
    {
        return a.Id.Equals(b.Id) && a.Number == b.Number && a.Enum == b.Enum;
    }
}
