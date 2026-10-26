using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;

namespace HelloWorld
{
    public struct DuelChatLine
    {
        public int Sequence;
        public ulong Sender;
        public FixedString64Bytes Name;
        public FixedString512Bytes Message;
    }
    public sealed class DuelChatLog
    {
        public const int MaxLines=100,MaxCharacters=120,MaxBytes=480;
        public const double SendInterval=.6;
        private readonly List<DuelChatLine> history=new List<DuelChatLine>();
        private readonly Dictionary<ulong,double> lastSent=new Dictionary<ulong,double>();
        private int sequence;
        public IReadOnlyList<DuelChatLine> History=>history;
        public static string Normalize(string input)
        {
            var clean=new StringBuilder();
            foreach(char c in input??string.Empty)
            {
                if(char.IsWhiteSpace(c)){if(clean.Length>0&&clean[clean.Length-1]!=' ')clean.Append(' ');}
                else if(!char.IsControl(c)&&CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.Format)clean.Append(c);
            }
            var elements=StringInfo.GetTextElementEnumerator(clean.ToString().Trim());
            var result=new StringBuilder();int count=0,bytes=0;
            while(count<MaxCharacters&&elements.MoveNext())
            {
                string part=elements.GetTextElement();int size=Encoding.UTF8.GetByteCount(part);
                if(bytes+size>MaxBytes)break;
                result.Append(part);bytes+=size;count++;
            }
            return result.ToString().Trim();
        }
        public bool TryAppend(ulong sender,FixedString64Bytes name,FixedString512Bytes message,double now,out DuelChatLine line)
        {
            line=default;
            if(double.IsNaN(now)||double.IsInfinity(now)||lastSent.TryGetValue(sender,out double previous)&&now-previous<SendInterval)return false;
            string clean=Normalize(message.ToString());if(clean.Length==0)return false;
            lastSent[sender]=now;
            line=new DuelChatLine{Sequence=++sequence,Sender=sender,Name=name,Message=new FixedString512Bytes(clean)};
            if(history.Count>=MaxLines)history.RemoveAt(0);
            history.Add(line);return true;
        }
        public void Disconnect(ulong sender)=>lastSent.Remove(sender);
        public void Clear(){history.Clear();lastSent.Clear();sequence=0;}
    }
}
