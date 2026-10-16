using System.Globalization;
using System.Text;
namespace HelloWorld
{
    public static class NicknamePolicy
    {
        public const int MaxCharacters = 16, MaxBytes = 60;
        public static string Normalize(string input)
        {
            var clean = new StringBuilder();
            foreach (char c in input ?? string.Empty)
                if (!char.IsControl(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.Format && c != '<' && c != '>') clean.Append(c);
            var elements = StringInfo.GetTextElementEnumerator(clean.ToString().Trim());
            var result = new StringBuilder(); int count = 0, bytes = 0;
            while (count < MaxCharacters && elements.MoveNext())
            {
                string part = elements.GetTextElement(); int size = Encoding.UTF8.GetByteCount(part);
                if (bytes + size > MaxBytes) break;
                result.Append(part); bytes += size; count++;
            }
            return result.ToString().Trim();
        }
        public static string Resolve(string input, int joinOrder)
        {
            string name = Normalize(input);
            return name.Length == 0 ? "player" + joinOrder.ToString(CultureInfo.InvariantCulture) : name;
        }
    }
}
