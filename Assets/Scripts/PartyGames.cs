using System;
namespace HelloWorld
{
    public enum PartyMiniGame : byte { Countdown, QuickHands, Signal, Greed, Memory }
    // Only implemented games belong here; the selection reel must never promise a fake game.
    public static class PartyGames
    {
        public const double SpinDuration=4.8, SelectionDuration=5.8;
        private static readonly PartyMiniGame[] games={PartyMiniGame.Countdown,PartyMiniGame.QuickHands,PartyMiniGame.Signal,PartyMiniGame.Greed,PartyMiniGame.Memory};
        public static int Count=>games.Length;
        public static PartyMiniGame At(int index)=>games[(index%games.Length+games.Length)%games.Length];
        public static PartyMiniGame Choose(Random random,PartyMiniGame? previous=null)
        {
            int eligible=0;foreach(var game in games)if(games.Length==1||game!=previous)eligible++;
            int pick=random.Next(0,eligible);
            foreach(var game in games)if((games.Length==1||game!=previous)&&pick--==0)return game;
            return games[0];
        }
        public static int IndexOf(PartyMiniGame game)=>Array.IndexOf(games,game);
        public static bool IsValid(PartyMiniGame game)=>System.Array.IndexOf(games,game)>=0;
        public static string Name(PartyMiniGame game)=>game==PartyMiniGame.Countdown?"초읽기":game==PartyMiniGame.QuickHands?"퀵핸즈":game==PartyMiniGame.Signal?"신호등":game==PartyMiniGame.Greed?"욕심의 대가":game==PartyMiniGame.Memory?"뭐였더라?":"";
    }
}
