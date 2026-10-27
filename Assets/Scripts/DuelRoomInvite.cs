using System;
using System.Net;
using System.Text;
using UnityEngine;
namespace HelloWorld
{
    // A LAN-only endpoint invitation, not a credential or an internet relay code.
    public static class DuelRoomInvite
    {
        private const string Prefix="QH1.";
        [Serializable] private sealed class Payload {public DuelLanRoom room;public string[] addresses;}
        public static string Encode(DuelLanRoom room)
        {
            var addresses=DuelLanRooms.LocalAddresses();if(room==null||addresses.Count==0)return "";
            return Prefix+Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Payload{room=room,addresses=addresses.ConvertAll(a=>a.ToString()).ToArray()}))).TrimEnd('=').Replace('+','-').Replace('/','_');
        }
        public static bool TryDecode(string text,out DuelLanRoom room,out string error)
        {
            room=null;error="초대 코드를 확인하세요.";text=(text??"").Trim();
            if(text.Length>4096||!text.StartsWith(Prefix,StringComparison.Ordinal))return false;
            try
            {
                string encoded=text.Substring(Prefix.Length).Replace('-','+').Replace('_','/');encoded=encoded.PadRight((encoded.Length+3)/4*4,'=');
                var payload=JsonUtility.FromJson<Payload>(new UTF8Encoding(false,true).GetString(Convert.FromBase64String(encoded)));
                var r=payload?.room;
                if(r==null||!DuelLanRooms.HexId(r.id)||r.port<1||r.port>65535||r.max!=6||r.title==null||r.title.Length>40||r.count<1||r.count>6||!DuelRoomCode.TryNormalize(r.code,out var code)||code!=r.code||payload.addresses==null||payload.addresses.Length<1||payload.addresses.Length>32)return false;
                if(r.protocol!=DuelLanRooms.Protocol){error="서로 다른 게임 버전입니다. 같은 최신 빌드로 실행하세요.";return false;}
                var local=DuelLanRooms.LocalAddresses();int best=-1;string selected=null;
                foreach(string address in payload.addresses)
                {
                    if(!IPAddress.TryParse(address,out var ip)||!DuelLanRooms.IsLanAddress(ip))return false;
                    int score=0;var remote=ip.GetAddressBytes();
                    foreach(var own in local)
                    {
                        var bytes=own.GetAddressBytes();int common=0;
                        for(int i=0;i<4&&remote[i]==bytes[i];i++)common++;
                        score=Math.Max(score,common);
                    }
                    if(score>best){best=score;selected=ip.ToString();}
                }
                r.address=selected;room=r;error="";return true;
            }
            catch(Exception e)when(e is ArgumentException||e is FormatException||e is DecoderFallbackException){return false;}
        }
    }
}
