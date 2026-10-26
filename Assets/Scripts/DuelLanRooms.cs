using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
namespace HelloWorld
{
    [Serializable] public sealed class DuelLanRoom
    {
        public string nonce,id,title;
        public int port,count,max,protocol;
        public bool locked,joinable;
        [NonSerialized] public string address;
        [NonSerialized] public double seenAt;
    }
    // Discovery only. The advertised IP is never trusted; use the UDP response's source address.
    public sealed class DuelLanRooms:IDisposable
    {
        public const int Protocol=52,FirstPort=47770,PortCount=16;
        private const string Query="QUICKHANDS_QUERY_48:",Reply="QUICKHANDS_ROOM_48:";
        private UdpClient host,browser;
        private string nonce;
        private double nextQuery;
        private readonly Dictionary<string,DuelLanRoom> rooms=new Dictionary<string,DuelLanRoom>();
        private readonly List<IPAddress> broadcasts=new List<IPAddress>();
        public string Error {get;private set;}="";
        public int Revision {get;private set;}
        public bool Browsing=>browser!=null;
        public bool Hosting=>host!=null;
        public IReadOnlyCollection<DuelLanRoom> Rooms=>rooms.Values;
        public bool StartHosting()
        {
            StopHosting();Error="";
            for(int i=0;i<PortCount;i++)try
            {
                var socket=new UdpClient(AddressFamily.InterNetwork);socket.ExclusiveAddressUse=true;
                try{socket.Client.Bind(new IPEndPoint(IPAddress.Any,FirstPort+i));socket.Client.Blocking=false;host=socket;return true;}
                catch{socket.Dispose();throw;}
            }
            catch(SocketException){ }
            Error="방 검색용 포트를 열지 못했습니다.";return false;
        }
        public bool Search(double now)
        {
            StopBrowsing();Error="";nonce=Guid.NewGuid().ToString("N");rooms.Clear();Revision++;
            try
            {
                browser=new UdpClient(0){EnableBroadcast=true};browser.Client.Blocking=false;
                broadcasts.Clear();broadcasts.Add(IPAddress.Broadcast);broadcasts.Add(IPAddress.Loopback);
                foreach(var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if(nic.OperationalStatus!=OperationalStatus.Up)continue;
                    foreach(var u in nic.GetIPProperties().UnicastAddresses)
                        if(u.Address.AddressFamily==AddressFamily.InterNetwork&&u.IPv4Mask!=null&&!IPAddress.IsLoopback(u.Address))
                        {
                            var a=u.Address.GetAddressBytes();var m=u.IPv4Mask.GetAddressBytes();
                            for(int i=0;i<4;i++)a[i]=(byte)(a[i]|~m[i]);var ip=new IPAddress(a);
                            if(!broadcasts.Contains(ip))broadcasts.Add(ip);
                        }
                }
                nextQuery=now;return true;
            }
            catch(Exception e)when(e is SocketException||e is NetworkInformationException)
            {StopBrowsing();Error="같은 네트워크의 방을 검색하지 못했습니다.";return false;}
        }
        public void Tick(DuelLanRoom advertised,double now)
        {
            if(host!=null)for(int n=0;n<64&&host.Available>0;n++)try
            {
                var source=new IPEndPoint(IPAddress.Any,0);byte[] data=host.Receive(ref source);
                if(advertised==null||data.Length!=Query.Length+32)continue;
                string request=Encoding.ASCII.GetString(data);if(!request.StartsWith(Query,StringComparison.Ordinal))continue;
                string token=request.Substring(Query.Length);if(!HexId(token))continue;
                advertised.nonce=token;byte[] answer=Encoding.UTF8.GetBytes(Reply+JsonUtility.ToJson(advertised));
                if(answer.Length<=1024)host.Send(answer,answer.Length,source);
            }catch(SocketException){break;}
            if(browser==null)return;
            if(now>=nextQuery)
            {
                nextQuery=now+1.5;byte[] request=Encoding.ASCII.GetBytes(Query+nonce);
                foreach(var ip in broadcasts)for(int p=0;p<PortCount;p++)try{browser.Send(request,request.Length,new IPEndPoint(ip,FirstPort+p));}catch(SocketException){ }
            }
            for(int n=0;n<128&&browser.Available>0;n++)try
            {
                var source=new IPEndPoint(IPAddress.Any,0);byte[] data=browser.Receive(ref source);
                if(data.Length>1024||data.Length<=Reply.Length)continue;
                string json=new UTF8Encoding(false,true).GetString(data);if(!json.StartsWith(Reply,StringComparison.Ordinal))continue;
                var room=JsonUtility.FromJson<DuelLanRoom>(json.Substring(Reply.Length));
                if(room==null||room.nonce!=nonce||room.protocol!=Protocol||!HexId(room.id)||room.title==null||room.title.Length>40||room.port<1||room.port>65535||room.max!=6||room.count<1||room.count>6)continue;
                room.address=source.Address.ToString();room.seenAt=now;
                string key=room.id;
                if(!rooms.ContainsKey(key)&&rooms.Count>=64)continue;
                if(!rooms.TryGetValue(key,out var old)||old.title!=room.title||old.count!=room.count||old.port!=room.port||old.joinable!=room.joinable||old.locked!=room.locked)Revision++;
                rooms[key]=room;
            }catch(Exception e)when(e is SocketException||e is ArgumentException||e is DecoderFallbackException){ }
            var expired=new List<string>();foreach(var pair in rooms)if(now-pair.Value.seenAt>6)expired.Add(pair.Key);
            foreach(var key in expired){rooms.Remove(key);Revision++;}
        }
        public static bool HexId(string value)
        {if(value==null||value.Length!=32)return false;foreach(char c in value)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;return true;}
        public void StopHosting(){host?.Dispose();host=null;}
        public void StopBrowsing(){browser?.Dispose();browser=null;}
        public void Dispose(){StopHosting();StopBrowsing();rooms.Clear();}
    }
}
