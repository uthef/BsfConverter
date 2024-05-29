using System.Diagnostics;
using BsfConverter.Core;

namespace BsfConverter.Test;

public class BasicFunctionalityTests
{
    private MemoryStream _memStream;
    private BsfMapper _mapper;
    private BsfDumper _dumper;
    private string _currentTest = "";

    [SetUp]
    public void Setup()
    {
        _currentTest = "";
        _memStream = new MemoryStream();
        _mapper = new BsfMapper();
        _dumper = new BsfDumper();
    }

    [TearDown]
    public void TearDown()
    {
        using (var fs = new FileStream($"{_currentTest}.log.bsf", FileMode.Create, FileAccess.Write))
        {
            _memStream.Position = 0;
            _memStream.CopyTo(fs);
            fs.Flush();
        }

        _memStream.Close();
        _memStream.Dispose();
    }

    [Test]
    public void ComplexTest()
    {
        _currentTest = nameof(ComplexTest);
        TestContext.WriteLine(_currentTest);

        DateTime dt;
        TimeSpan ts;

        _dumper.RegisterType<Guid>((obj) => ((Guid)obj).ToByteArray());

        var inSubClasses = new TestClass2[] 
        { 
            new(Guid.NewGuid(), 3.2, TestEnum.Value1), 
            new(Guid.NewGuid(), 46.1, TestEnum.Value2),
            new(Guid.NewGuid(), 23.1, TestEnum.Value3),
            new(Guid.NewGuid(), -32.09, TestEnum.Value4),
        };

        var inClass = new TestClass1(inSubClasses, true);
        
        dt = DateTime.Now;
        _dumper.Serialize(inClass, _memStream);
        ts = DateTime.Now - dt;
        TestContext.WriteLine($"\tSerialization took {ts.TotalMilliseconds:0.####} ms");

        _memStream.Flush();
        _memStream.Position = 0;

        dt = DateTime.Now;
        var outClass = _mapper.Deserialize<TestClass1>(_memStream);
        ts = DateTime.Now - dt;
        TestContext.WriteLine($"\tDeserialization took {ts.TotalMilliseconds:0.####} ms");

        Assert.That(outClass, Is.Not.Null);
        Assert.That(TestClass1.AreEquivalent(inClass, outClass));
    }

    [Test]
    public void MultidimensionalListTest()
    {
        _currentTest = nameof(MultidimensionalListTest);
        TestContext.WriteLine(_currentTest);

        List<List<List<int>>> mdList =
        [
            [ 
                [], 
                [3, 4, 5], 
                [6, 7, 8]
            ],
            [ 
                [9, 10, 11], 
                [12, 13, 14], 
                [15, 16, 17]
            ],
            [ 
                [18, 19, 20], 
                [21, 22, 23], 
                [24, 25, 26]
            ],
        ];

        _dumper.Serialize(mdList, _memStream);

        _memStream.Flush();
        _memStream.Position = 0;
        var outMdArr = _mapper.Deserialize<int[][][]>(_memStream);
        Assert.That(outMdArr, Is.Not.Null);

        _memStream.Position = 0;
        var outMdList = _mapper.Deserialize<List<List<List<int>>>>(_memStream);
        Assert.That(outMdList, Is.Not.Null);

        Assert.Multiple(() => 
        {
            Assert.That(outMdArr, Is.EquivalentTo(mdList)); 
            Assert.That(outMdList, Is.EquivalentTo(mdList));
        });

        for (int i = 0; i < outMdArr.Length; i++)
        {
            for (int j = 0; j < outMdArr[i].Length; j++)
            {
                TestContext.Write("\t");

                if (outMdArr[i][j].Length == 0) TestContext.Write("<empty array>");

                for (int k = 0; k < outMdArr[i][j].Length; k++)
                {
                    TestContext.Write(outMdArr[i][j][k] + "\t");
                }

                TestContext.WriteLine();
            }
        }
    }
}
