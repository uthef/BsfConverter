using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using BsfConverter.Core.Attributes;
using BsfConverter.Core.Exceptions;

namespace BsfConverter.Core;

public class BsfMapper
{
    public bool LongEnums = true;

    public T? Deserialize<T>(Stream stream)
    {            
        var type = typeof(T);
        var res = Deserialize(stream, type);
        return (T?)res;
    }
    
    private object? Deserialize(Stream stream, Type type)
    {
        if (!stream.CanRead)
                throw new BsfSerializationException("The provided stream is not readable");

        if (type.IsPrimitive)
        {
            var value = DeserializePrimitive(stream, type);
            return value;
        }

        if (type == typeof(string))
        {
            var str = DeserializeString(stream);
            return str;
        }

        if (type == typeof(byte[]))
        {
            return ReadSequence(stream);
        }

        var modelAttr = type.GetCustomAttribute<BsfModelAttribute>() is {};

        if (typeof(IEnumerable).IsAssignableFrom(type) && !modelAttr)
        {
            var genericArgs = type.GetGenericArguments();
            var assumeCollection = genericArgs.Length > 0;
            var staticEnumType = typeof(Enumerable);

            string convMethodName;

            if (type.HasElementType)
            {
                convMethodName = nameof(Enumerable.ToArray);
            }
            else if (type.GetGenericTypeDefinition() == typeof(List<>))
            {
                convMethodName = nameof(Enumerable.ToList);
            }
            else throw new BsfDeserializationException($"Unsupported type {type.Name}");

            var subType1 = assumeCollection ? genericArgs[0] : type.GetElementType();

            if (type.IsArray && type.GetArrayRank() == 2)
            {
                subType1 = subType1!.MakeArrayType();
            }
            else if (type.IsArray && type.GetArrayRank() > 2)
            {
                subType1 = subType1!.MakeArrayType(type.GetArrayRank() - 1);
            }

            if (subType1 is null) throw new UnreachableException();

            var enumerable = DeserializeEnumerable(stream, subType1);

            var typedEnum = staticEnumType
                .GetMethod(nameof(Enumerable.Cast))!
                .MakeGenericMethod(subType1)
                .Invoke(null, [enumerable])!;

            var finalObj = staticEnumType
                .GetMethod(convMethodName)!
                .MakeGenericMethod(subType1)
                .Invoke(null, [typedEnum]);
 
            return finalObj;
        }

        if (type.IsEnum)
        {
            return Deserialize(stream, LongEnums ? typeof(ushort) : typeof(byte));
        }

        if (type.IsClass || (type.IsValueType && modelAttr))
        {
            return DeserializeClass(stream, type);
        }

        throw new BsfDeserializationException($"Unsupported type {type.Name}");
    }

    private string? DeserializeString(Stream stream)
    {
        var buffer = ReadSequence(stream);
        if (buffer.Length == 0) return null;
        return Encoding.UTF8.GetString(buffer);
    }

    private object? DeserializePrimitive(Stream stream, Type type)
    {
        var bytes = ReadSequence(stream);

        if (bytes.Length == 0) return null;

        if (type == typeof(byte))
        {
            return bytes[0];
        }

        if (type == typeof(short))
        {
            return BitConverter.ToInt16(bytes);
        }

        if (type == typeof(ushort))
        {
            return BitConverter.ToUInt16(bytes);
        }

        if (type == typeof(int))
        {
            return BitConverter.ToInt32(bytes);
        }

        if (type == typeof(uint))
        {
            return BitConverter.ToUInt32(bytes);
        }

        if (type == typeof(long))
        {
            return BitConverter.ToInt64(bytes);
        }

        if (type == typeof(ulong))
        {
            return BitConverter.ToUInt64(bytes);
        }

        if (type == typeof(float))
        {
            return BitConverter.ToSingle(bytes);
        }

        if (type == typeof(double))
        {
            return BitConverter.ToDouble(bytes);
        }

        if (type == typeof(char))
        {
            return BitConverter.ToChar(bytes);
        }

        if (type == typeof(bool))
        {
            return BitConverter.ToBoolean(bytes);
        }

        throw new BsfDeserializationException($"Unsupported type {type.Name}");
    }

    private IEnumerable DeserializeEnumerable(Stream stream, Type type)
    {
        var posBeforeHeader = stream.Position;
        ReadHeader(stream, out var mark, out var length);

        if (mark != 0 && (mark < ByteMarkUtils.Array || mark >= ByteMarkUtils.Dictionary)) 
            throw new BsfDeserializationException($"An array was expected. Position: {posBeforeHeader}");

        if (length == 0) yield break;

        for (long i = 0; i < length; i++)
        {
            yield return Deserialize(stream, type);
        }
    }

    private object? DeserializeClass(Stream stream, Type type)
    {
        var posBeforeHeader = stream.Position;
        ReadHeader(stream, out var mark, out var length);

        if (mark != 0 && mark < ByteMarkUtils.Dictionary) 
            throw new BsfDeserializationException($"A dictionary was expected. Position: {posBeforeHeader}");

        var preferredConstructors = 
            type.GetConstructors().Where(x => x.GetCustomAttribute<BsfConstructorAttribute>() is {}).ToArray();

        if (preferredConstructors.Length > 1) 
            throw new BsfDeserializationException("Only one constructor per class is allowed to be decorated with BsfConstructor attribute");

        var ctors = preferredConstructors.Length > 0 ? preferredConstructors : type.GetConstructors();

        if (ctors.Length == 0) 
            throw new BsfDeserializationException($"Type {type.Name} does not have a public constructor");
        
        var ctor = ctors[0];
        
        if (ctor.GetParameters().Length == 0) return ctor.Invoke([]);

        object?[] paramValues = new object?[ctor.GetParameters().Length];

        if (ctor.GetParameters().Length != length) 
            throw new BsfDeserializationException($"Parameter count does not match for type {type.Name}");

        var comp = StringComparison.OrdinalIgnoreCase;

        for (long i = 0; i < length * 2 - 1; i++)
        {
            var key = DeserializeString(stream);

            var parameters = 
                ctors[0].GetParameters().Where(x => string.Equals(x.Name, key, comp));
            
            if (!parameters.Any())
                throw new BsfDeserializationException($"Type {type.Name} constructor does not have the parameter named {key} ");
            
            var paramInfo = parameters.First();

            var value = Deserialize(stream, paramInfo.ParameterType);
            paramValues[paramInfo.Position] = value;
            i++;
        }

        return ctor.Invoke(paramValues);
    }

    private byte[] Read(Stream stream, long length)
    {
        byte[] buffer = new byte[length];
        int bytesRead = stream.Read(buffer);

        if (bytesRead != length) 
            throw new BsfDeserializationException($"Unexpected end of stream. Position: {stream.Position}");
        
        return buffer;
    }

    private void ReadHeader(Stream stream, out byte mark, out long length)
    {
        byte[] buffer;
        int @byte = stream.ReadByte();

        if (@byte == -1) 
            throw new BsfDeserializationException($"Unexpected end of stream. Position: {stream.Position}");

        mark = (byte)@byte;
        
        length = ByteMarkUtils.GetLength(mark, out var isHeaderLength);

        if (length == 0)
        {
            mark = 0;
            length = 0;
            return;
        }

        if (isHeaderLength)
        {
            buffer = Read(stream, length);

            length = length switch 
            {
                1 => buffer[0],
                2 => BitConverter.ToUInt16(buffer),
                4 => BitConverter.ToUInt32(buffer),
                8 => BitConverter.ToInt64(buffer),
                _ => throw new UnreachableException()
            };

            if (length < 0)
            {
                throw new BsfDeserializationException($"Size header cannot be less than 0. Position: {stream.Position}");
            }
        }
    }

    private byte[] ReadSequence(Stream stream)
    {
        byte[] buffer;

        var posBeforeHeader = stream.Position;

        ReadHeader(stream, out var mark, out var length);

        if (mark >= ByteMarkUtils.Array) 
            throw new BsfDeserializationException($"Null, byte or byte sequence were expected. Position: {posBeforeHeader}");

        buffer = Read(stream, length);

        return buffer;
    }
}
