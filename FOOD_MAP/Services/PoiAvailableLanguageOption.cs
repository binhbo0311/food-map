namespace FOOD_MAP.Services;

public sealed class PoiAvailableLanguageOption
{
    public string LanguageCode { get; init; } = string.Empty;

    public string LanguageName { get; init; } = string.Empty;

    public string PromptLabel
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LanguageName))
            {
                return LanguageCode.ToUpperInvariant();
            }

            return $"{LanguageName} ({LanguageCode})";
        }
    }
}