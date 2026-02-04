using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdministrativeServices.Helpers
{
    public static class TextHelper
    {
        public static string NormalizeName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Remove extra spaces
            var name = Regex.Replace(input.Trim(), @"\s+", " ");

            // Convert to Title Case
            TextInfo textInfo = new CultureInfo("vi-VN", false).TextInfo;
            return textInfo.ToTitleCase(name.ToLower());
        }

        public static string RemoveSign(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string normalizedString = input.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
