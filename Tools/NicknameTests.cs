using System;
using System.Text;
using HelloWorld;
class NicknameTests
{
    static int count;
    static void Check(bool ok) { count++; if (!ok) throw new Exception("Nickname check " + count); }
    static void Main()
    {
        Check(NicknamePolicy.Resolve(null, 1) == "player1");
        Check(NicknamePolicy.Resolve(" \t\r\n ", 5) == "player5");
        Check(NicknamePolicy.Resolve("  달빛고양이  ", 3) == "달빛고양이");
        Check(NicknamePolicy.Normalize("a\nb\tc") == "abc");
        Check(!NicknamePolicy.Normalize("<b>가나다</b>").Contains("<"));
        Check(NicknamePolicy.Normalize(new string('가', 100)).Length == 16);
        Check(Encoding.UTF8.GetByteCount(NicknamePolicy.Normalize(string.Concat(System.Linq.Enumerable.Repeat("😀", 30)))) <= 60);
        Check(NicknamePolicy.Normalize("\u202eabc\u200b") == "abc");
        var rng = new Random(12);
        for (int i = 0; i < 100; i++)
        {
            var input = new StringBuilder();
            for (int j = 0; j < 70; j++) input.Append(rng.Next(2) == 0 ? "한" : "😀");
            string normalized = NicknamePolicy.Normalize(input.ToString());
            Check(Encoding.UTF8.GetByteCount(normalized) <= 60);
            Check(new UTF8Encoding(false, true).GetString(Encoding.UTF8.GetBytes(normalized)) == normalized);
        }
        Console.WriteLine("PASS " + count + " nickname assertions");
    }
}
