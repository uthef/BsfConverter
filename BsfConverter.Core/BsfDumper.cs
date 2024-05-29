using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Reflection;
using BsfConverter.Core.Attributes;
using BsfConverter.Core.Exceptions;

[assembly: InternalsVisibleTo("BsfConverter.Console")]
[assembly: InternalsVisibleTo("BsfConverter.Test")]

namespace BsfConverter.Core;

public class BsfDumper
{
    public delegate byte[] CustomTypeSerializer(object value);
    private readonly Dictionary<Type, CustomTypeSerializer> _typeHandlers = [];
    public bool IncludePropertiesOnly = false;
    public bool LongEnums = true;

    public void RegisterType<T>(CustomTypeSerializer handler)
    {
        _typeHandlers[typeof(T)] = handler;
    }

    public void Serialize(object? value, Stream stream)
    {
        if (!stream.CanWrite) 
            throw new BsfSerializationException($"Stream is not writable. Position: {stream.Position}");
        
        if (value is null) 
        {
            WriteSequence(stream, []);
            return;
        }

        var type = value.GetType();

        if (SerializeRegisteredType(value, stream)) return;
        
        if (type.IsPrimitive)
        {
            SerializePrimitive(value, stream);
            return;
        }

        if (value is string str)
        {
            SerializeString(str, stream);
            return;
        }

        if (value is byte[] byteArray)
        {
            WriteSequence(stream, byteArray);
            return;
        }

        var modelAttr = type.GetCustomAttribute<BsfModelAttribute>() is {};

        if (typeof(IEnumerable).IsAssignableFrom(type) && !modelAttr)
        {  
            var dict = value is IDictionary ? (IDictionary)value : null;
            var enumerable = dict is {} ? null : (IEnumerable)value;

            if (enumerable is {}) SerializeCollection(enumerable, stream);
            else SerializeDictionary(dict!, stream);

            return;
        }

        if (type.IsEnum)
        {
            if (LongEnums)
                SerializePrimitive((ushort)(int)value, stream);
            else 
                SerializePrimitive((byte)(int)value, stream);
            
            return;
        }

        if (type.IsClass || (type.IsValueType && modelAttr))
        {
            SerializeClass(value, stream);
            return;
        }

        throw new BsfSerializationException($"Unsupported type {type.Name}");
    }

    private void SerializePrimitive(object val, Stream stream)
    {
            if (val is byte @byte)
            {
                WriteSequence(stream, [@byte]);
                return;
            }

            if (val is short @short)
            {
                WriteSequence(stream, BitConverter.GetBytes(@short));
                return;
            }

            if (val is ushort @ushort)
            {
                WriteSequence(stream, BitConverter.GetBytes(@ushort));
                return;
            }

            if (val is int @int)
            {
                WriteSequence(stream, BitConverter.GetBytes(@int));
                return;
            }

            if (val is uint @uint)
            {
                WriteSequence(stream, BitConverter.GetBytes(@uint));
                return;
            }
   
            if (val is long @long)
            {
                WriteSequence(stream, BitConverter.GetBytes(@long));
                return;
            }

            if (val is ulong @ulong)
            {
                WriteSequence(stream, BitConverter.GetBytes(@ulong));
                return;
            }

            if (val is float @float)
            {
                WriteSequence(stream, BitConverter.GetBytes(@float));
                return;
            }

            if (val is double @double)
            {
                WriteSequence(stream, BitConverter.GetBytes(@double));
                return;
            }

            if (val is char @char)
            {
                WriteSequence(stream, BitConverter.GetBytes(@char));
                return;
            }

            if (val is bool @bool)
            {
                WriteSequence(stream, BitConverter.GetBytes(@bool));
                return;
            }

            throw new BsfSerializationException($"Unsupported type {val.GetType().Name}");
    }

    private bool SerializeRegisteredType(object val, Stream stream)
    {
        _typeHandlers.TryGetValue(val.GetType(), out var handler);
        
        if (handler is {}) 
        {
            var bytes = handler(val);
            WriteSequence(stream, bytes);

            return true;
        }

        return false;
    }

    private void SerializeClass(object obj, Stream stream)
    {
        if (!stream.CanSeek)
            throw new BsfSerializationException("The provided stream does not support seeking");

        long count = 0;
        var type = obj.GetType();
        var headerPos = stream.Position;
        stream.Position++;

        var properties = obj.GetType().GetProperties();

        foreach (var property in properties)
        {
            if (property.GetCustomAttribute<BsfIgnoreAttribute>() is {}) continue;

            SerializeString(property.Name, stream);
            
            Serialize(property.GetValue(obj), stream);
            count++;
        }

        if (!IncludePropertiesOnly)
        {
            var fields = type.GetFields();

            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<BsfIgnoreAttribute>() is {}) continue;

                SerializeString(field.Name, stream);
                Serialize(field.GetValue(obj), stream);
                count++;
            }
        }

        var endPos = stream.Position;
        stream.Position = headerPos;
        var mark = ByteMarkUtils.GetMarkForLength(ByteMarkUtils.Dictionary, count);

        WriteHeader(stream, mark);

        stream.Position = endPos;
    }

    private void SerializeCollection(IEnumerable enumerable, Stream stream)
    {
        if (!stream.CanSeek)
            throw new BsfSerializationException("The provided stream does not support seeking");

        long count = 0;

        var headerPos = stream.Position;
        stream.Position++;
        
        foreach (var item in enumerable)
        {
            Serialize(item, stream);
            count++;
        }

        var endPos = stream.Position;
        stream.Position = headerPos;
        var mark = ByteMarkUtils.GetMarkForLength(ByteMarkUtils.Array, count);

        WriteHeader(stream, mark);

        stream.Position = endPos;
    }

    private void SerializeDictionary(IDictionary dictionary, Stream stream)
    {
        var mark = ByteMarkUtils.GetMarkForLength(ByteMarkUtils.Dictionary, dictionary.Count);
        WriteHeader(stream, mark);

        foreach (var key in dictionary.Keys)
        {
            var dictVal = dictionary[key];
            Serialize(key, stream);
            Serialize(dictVal, stream);
        }
    }

    private static void SerializeString(string str, Stream stream)
    {
        WriteSequence(stream, Encoding.UTF8.GetBytes(@str));
    }

    private static void WriteHeader(Stream stream, byte mark)
    {
        var length = ByteMarkUtils.GetLength(mark, out var isHeaderLength);

        stream.WriteByte(mark);

        if (isHeaderLength)
        {
            byte[] sizeBytes = length switch
            {
                1 => [(byte)length],
                2 => BitConverter.GetBytes((ushort)length),
                3 => BitConverter.GetBytes((uint)length),
                4 => BitConverter.GetBytes(length),
                _ => throw new UnreachableException()
            };

            stream.Write(sizeBytes);
        }
    }

    private static void WriteSequence(Stream stream, byte[] sequence)
    {
        var mark = ByteMarkUtils.GetMarkForLength(ByteMarkUtils.Sequence, sequence.LongLength);
        WriteHeader(stream, mark);
        stream.Write(sequence);
    }
}
