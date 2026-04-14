using System.Globalization;
using System.Text;

namespace FOOD_MAP.Shared.Services;

public static class TextInputNormalizer
{
    public static string NormalizeSingleLine(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var normalized = rawValue.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            // Loại bỏ ký tự điều khiển có thể gây lỗi lưu/truy vấn khi nhập từ nhiều bộ gõ khác nhau.
            if (char.IsControl(character))
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    public static string? NormalizeNullableSingleLine(string? rawValue)
    {
        var normalized = NormalizeSingleLine(rawValue);
        return normalized.Length == 0 ? null : normalized;
    }

    public static string NormalizeMultiline(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var normalized = rawValue
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Normalize(NormalizationForm.FormC);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            // Giữ xuống dòng/tab cho nội dung mô tả dài, nhưng vẫn loại control ký tự không hợp lệ.
            if (char.IsControl(character) && character != '\n' && character != '\t')
            {
                continue;
            }

            builder.Append(character);
        }

        var lines = builder
            .ToString()
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();

        return string.Join(Environment.NewLine, lines).Trim();
    }

    public static string? NormalizeNullableMultiline(string? rawValue)
    {
        var normalized = NormalizeMultiline(rawValue);
        return normalized.Length == 0 ? null : normalized;
    }

    public static bool TryParseDecimal(string rawValue, out decimal parsedValue)
    {
        return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
            || decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.CurrentCulture, out parsedValue);
    }
}
