using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;

namespace AdministrativeServices.Helpers
{
    public static class TextHelper
    {
        public static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            // Remove extra spaces
            string trimmed = Regex.Replace(name.Trim(), @"\s+", " ");
            
            // Uppercase
            return trimmed.ToUpperInvariant();
        }

        // Example: "NGUYEN VAN A"
    }
}
