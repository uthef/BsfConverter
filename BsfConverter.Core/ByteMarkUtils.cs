using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("BsfConverter.Console")]
[assembly: InternalsVisibleTo("BsfConverter.Test")]

namespace BsfConverter.Core;

internal static class ByteMarkUtils
{
    public const byte Null = 0,  Single = 1, Sequence = 2,
    Array = 86, Dictionary = 170, 
    SequenceSoftLimit = Array - 4,
    ArraySoftLimit = Dictionary - 4;

    public static long GetLength(byte mark, out bool isHeaderLength)
    {
        isHeaderLength = false;

        int diff;

        if (mark < 2) return mark;
        if (mark < Array - 4) return mark - Sequence + 2;

        if (mark < Array)
        {
            isHeaderLength =  true;
            diff = mark - SequenceSoftLimit + 1;
        }
        else if (mark < ArraySoftLimit)
        {
            isHeaderLength = false;
            return mark - Array + 1;
        }
        else if (mark < Dictionary)
        {
            isHeaderLength = true;
            diff = mark - ArraySoftLimit + 1;
        }
        else if (mark < 252)
        {
            isHeaderLength = false;
            return mark - Dictionary + 1;
        }
        else 
        {
            isHeaderLength = true;
            diff = mark - 251;
        }

        return diff switch 
        {
            3 => 4,
            4 => 8,
            _ => diff
        };
    }

    public static byte GetMarkForLength(byte baseMark, long length)
    {
        if (length == 0) return 0;
        if (length == 1 && baseMark < Array) return 1;

        var range = baseMark >= Dictionary ? 82 : 80;

        if (length > uint.MaxValue) return (byte)(baseMark + range + 3);    
        if (length > ushort.MaxValue) return (byte)(baseMark + range + 2);     
        if (length > 255) return (byte)(baseMark + range + 1);
        if (length > range) return (byte)(baseMark + range);
        
        return (byte)(baseMark + length - (baseMark < Array ? 2 : 1));
    }
}
