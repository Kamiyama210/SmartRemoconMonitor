using System.Globalization;
using System.Text;

namespace SmartRemoconMonitor.Services.Export;

/// <summary>
/// CSV関連処理
/// </summary>
internal static class Csv
{
    /// <summary>
    /// カンマ・引用符・改行対策
    /// </summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuoting = value.AsSpan().IndexOfAny(",\"\r\n") >= 0;
        if (!needsQuoting)
        {
            return value;
        }

        return string.Concat("\"", value.Replace("\"", "\"\"", StringComparison.Ordinal), "\"");
    }

    /// <summary>
    /// カンマ追加。行末はCRLF。
    /// </summary>
    public static void AppendRow(StringBuilder builder, params string?[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(Escape(fields[i]));
        }

        builder.Append("\r\n");
    }

    /// <summary>
    /// 数値を小数点以下1桁にする。
    /// </summary>
    public static string Number(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
}
