using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HABIMON.Core
{
    public static class DailyHabitLogIdentity
    {
        public static string GetDocumentId(string habitId, string dateKey)
        {
            if (string.IsNullOrWhiteSpace(habitId))
                throw new ArgumentException("A habit id is required.", nameof(habitId));
            if (string.IsNullOrWhiteSpace(dateKey))
                throw new ArgumentException("A date key is required.", nameof(dateKey));

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(habitId + "\n" + dateKey));
                var builder = new StringBuilder("daily_", 70);
                foreach (byte value in digest)
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        public static bool MatchesDocumentId(string documentId, string habitId, string dateKey)
        {
            if (string.IsNullOrWhiteSpace(documentId) ||
                string.IsNullOrWhiteSpace(habitId) ||
                string.IsNullOrWhiteSpace(dateKey))
                return false;

            return string.Equals(documentId, GetDocumentId(habitId, dateKey), StringComparison.Ordinal);
        }
    }
}
