namespace HelloWorld
{
    public static class DuelRoomCode
    {
        public const int MinLength=4,MaxLength=12;
        public static bool TryNormalize(string input,out string code)
        {
            code=(input??"").Trim();
            if(code.Length<MinLength||code.Length>MaxLength)return false;
            foreach(char c in code)if(!(c>='A'&&c<='Z'||c>='a'&&c<='z'||c>='0'&&c<='9'))return false;
            code=code.ToUpperInvariant();
            return true;
        }
    }
}
