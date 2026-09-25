namespace DataAccessObjects;

public static class TextNormalization
{
    // Mirrors the stored Normalized* columns: upper(btrim(value)), where btrim only strips spaces.
    public static string Normalize(string value) => value.Trim(' ').ToUpperInvariant();
}
