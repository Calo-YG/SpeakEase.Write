namespace SpeakEase.Write.Infrastructure.Text;

/// <summary>
/// 高性能 long 转 string 转换器。
/// </summary>
public static class LongToStringConverter
{
    private const char ZeroChar = '0';

    /// <summary>
    /// 将 long 值转换为字符串。
    /// </summary>
    public static string Convert(long value)
    {
        if (value == long.MinValue)
        {
            return "-9223372036854775808";
        }

        Span<char> buffer = stackalloc char[20];
        var index = buffer.Length;

        var isNegative = value < 0;
        ulong number = isNegative ? (ulong)(-value) : (ulong)value;

        do
        {
            buffer[--index] = (char)(number % 10 + ZeroChar);
            number /= 10;
        } while (number > 0);

        if (isNegative)
        {
            buffer[--index] = '-';
        }

        return buffer[index..].ToString();
    }
}
