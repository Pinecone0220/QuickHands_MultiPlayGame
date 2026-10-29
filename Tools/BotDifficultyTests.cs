using System;
using HelloWorld;
public static class BotDifficultyTests
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    public static void Main()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(1));
        Check(d.Join(0,0),"host joins");
        Check(d.AddBot(1,0,AiDifficulty.Easy),"easy bot joins");
        Check(d.AddBot(2,0,AiDifficulty.Normal),"normal bot joins");
        Check(d.AddBot(3,0,AiDifficulty.Hard),"hard bot joins");
        Check(d.BotDifficulty(1)==AiDifficulty.Easy&&d.BotDifficulty(2)==AiDifficulty.Normal&&d.BotDifficulty(3)==AiDifficulty.Hard,"individual difficulty retained");
        Check(d.AiInterval(1,.5)>d.AiInterval(2,.5)&&d.AiInterval(2,.5)>d.AiInterval(3,.5),"action intervals reflect individual difficulty");
        Check(d.AiErrorChanceFor(1)>d.AiErrorChanceFor(2)&&d.AiErrorChanceFor(2)>d.AiErrorChanceFor(3),"error chances reflect individual difficulty");
        Check(!d.AddBot(1,0,AiDifficulty.Hard)&&d.BotDifficulty(1)==AiDifficulty.Easy,"occupied seat cannot change bot difficulty");
        Check(!d.AddBot(4,0,(AiDifficulty)99)&&!d.IsBot(4),"invalid difficulty rejected");
        var solo=new DuelDirector(DuelMode.AI,new Random(1),AiDifficulty.Hard);
        Check(solo.Join(0,0)&&solo.Join(1,0)&&solo.BotDifficulty(1)==AiDifficulty.Hard,"solo AI keeps selected default");
        Console.WriteLine("Bot difficulty: "+checks+" checks passed.");
    }
}
