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
        public string nonce,id,title,code;
        public int port,count,max,protocol;
        public bool locked,joinable;
        [NonSerialized] public string address;
        [NonSerialized] public double seenAt;
    }
    // Discovery has no game authority. Only response-source IPv4 addresses are accepted.
    public sealed class DuelLanRooms:IDisposable
    {
        public const int Protocol=55,FirstPort=47770,PortCount=16;
        public const double LookupDuration=8;
        private const string Query="QUICKHANDS_QUERY_48:",Reply="QUICKHANDS_ROOM_48:";
        private sealed class Channel
        {
            public UdpClient socket;
            public IPAddress local,broadcast;
            public readonly List<IPAddress> peers=new List<IPAddress>();
            public int cursor;
            public double restart;
        }
        private UdpClient host;
        private readonly List<Channel> channels=new List<Channel>();
        private readonly Dictionary<string,DuelLanRoom> rooms=new Dictionary<string,DuelLanRoom>();
        private readonly HashSet<string> incompatible=new HashSet<string>();
        private string nonce;
        private double nextQuery,startedAt;
        private int nextChannel;
        // QA can independently exercise the unicast fallback without broadcasts or loopback.
        internal bool BroadcastEnabled=true,LoopbackEnabled=true;
        public string Error {get;private set;}="";
        public int Revision {get;private set;}
        public int IncompatibleCount=>incompatible.Count;
        public bool Browsing=>channels.Count>0;
        public bool Hosting=>host!=null;
        public IReadOnlyCollection<DuelLanRoom> Rooms=>rooms.Values;
        private static UdpClient Socket(IPAddress address,int port)
        {
            var socket=new UdpClient(AddressFamily.InterNetwork);
            try
            {
                socket.ExclusiveAddressUse=true;socket.EnableBroadcast=true;
                socket.Client.Bind(new IPEndPoint(address,port));socket.Client.Blocking=false;
                // Windows otherwise surfaces asynchronous UDP ICMP replies as receive failures.
                if(Environment.OSVersion.Platform==PlatformID.Win32NT)
                    socket.Client.IOControl(unchecked((IOControlCode)(int)0x9800000C),new byte[4],null);
                return socket;
            }
            catch{socket.Dispose();throw;}
        }
        public bool StartHosting()
        {
            StopHosting();Error="";
            for(int i=0;i<PortCount;i++)try{host=Socket(IPAddress.Any,FirstPort+i);Debug.Log("[LAN] Discovery listening on "+(FirstPort+i));return true;}catch(SocketException){ }
            Error="방 검색용 포트를 열지 못했습니다.";return false;
        }
        public static List<IPAddress> LocalAddresses()
        {
            var result=new List<IPAddress>();
            try
            {
                foreach(var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if(nic.OperationalStatus!=OperationalStatus.Up||nic.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;
                    foreach(var u in nic.GetIPProperties().UnicastAddresses)
                        if(IsLanAddress(u.Address)&&!result.Contains(u.Address)&&result.Count<32)result.Add(u.Address);
                }
            }
            catch(NetworkInformationException){ }
            return result;
        }
        public static bool IsLanAddress(IPAddress ip)
        {
            if(ip==null||ip.AddressFamily!=AddressFamily.InterNetwork||IPAddress.IsLoopback(ip))return false;
            var b=ip.GetAddressBytes();return b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==169&&b[1]==254;
        }
        public bool Search(double now)
        {
            StopBrowsing();Error="";nonce=Guid.NewGuid().ToString("N");rooms.Clear();incompatible.Clear();Revision++;startedAt=now;
            try
            {
                foreach(var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if(nic.OperationalStatus!=OperationalStatus.Up)continue;
                    foreach(var u in nic.GetIPProperties().UnicastAddresses)
                    {
                        if(!IsLanAddress(u.Address)||u.IPv4Mask==null||channels.Count>=8)continue;
                        bool exists=false;foreach(var c in channels)if(c.local.Equals(u.Address))exists=true;if(exists)continue;
                        try
                        {
                            var channel=new Channel{local=u.Address,socket=Socket(u.Address,0)};
                            var a=u.Address.GetAddressBytes();var m=u.IPv4Mask.GetAddressBytes();var broadcast=new byte[4];
                            for(int i=0;i<4;i++)broadcast[i]=(byte)(a[i]|~m[i]);channel.broadcast=new IPAddress(broadcast);
                            // At most 512 hosts per interface, never an unbounded /16 or wider scan.
                            uint value=Number(u.Address),mask=Number(u.IPv4Mask),scanMask=mask|0xfffffe00u;
                            uint first=value&scanMask,last=first|~scanMask;
                            for(uint address=first;address<=last;address++)
                                if(address!=(value&mask)&&address!=Number(channel.broadcast))channel.peers.Add(Address(address));
                            channels.Add(channel);
                        }
                        catch(SocketException e){Debug.LogWarning("[LAN] Interface bind failed: "+u.Address+" / "+e.SocketErrorCode);}
                    }
                }
            }
            catch(NetworkInformationException e){Debug.LogWarning("[LAN] Interface enumeration: "+e.ErrorCode);}
            if(LoopbackEnabled)try{channels.Add(new Channel{local=IPAddress.Loopback,broadcast=IPAddress.Loopback,socket=Socket(IPAddress.Loopback,0)});}catch(SocketException){ }
            nextQuery=now;nextChannel=0;
            Debug.Log("[LAN] Search started, interfaces="+channels.Count+", protocol="+Protocol);
            if(Browsing)return true;Error="LAN 검색용 네트워크를 열지 못했습니다. 네트워크 연결을 확인하세요.";return false;
        }
        private static uint Number(IPAddress ip){var b=ip.GetAddressBytes();return (uint)b[0]<<24|(uint)b[1]<<16|(uint)b[2]<<8|b[3];}
        private static IPAddress Address(uint n)=>new IPAddress(new[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n});
        private void Send(Channel c,byte[] request,IPAddress ip,int port)
        {
            try{c.socket.Send(request,request.Length,new IPEndPoint(ip,port));}
            catch(SocketException e){if(Error.Length==0){Error="일부 LAN 검색 요청을 보내지 못했습니다. 초대 코드로 직접 참가할 수 있습니다.";Debug.LogWarning("[LAN] Send: "+e.SocketErrorCode);}}
        }
        public void Tick(DuelLanRoom advertised,double now)
        {
            if(host!=null)for(int n=0;n<256;n++)try
            {
                if(host.Available==0)break;
                var source=new IPEndPoint(IPAddress.Any,0);byte[] data=host.Receive(ref source);
                if(advertised==null||data.Length!=Query.Length+32)continue;
                string request=Encoding.ASCII.GetString(data);if(!request.StartsWith(Query,StringComparison.Ordinal))continue;
                string token=request.Substring(Query.Length);if(!HexId(token))continue;
                advertised.nonce=token;byte[] answer=Encoding.UTF8.GetBytes(Reply+JsonUtility.ToJson(advertised));
                if(answer.Length<=1024)host.Send(answer,answer.Length,source);
            }catch(SocketException){break;}
            if(!Browsing)return;
            byte[] query=Encoding.ASCII.GetBytes(Query+nonce);
            if(now>=nextQuery)
            {
                nextQuery=now+1;
                foreach(var c in channels)
                    if(BroadcastEnabled||IPAddress.IsLoopback(c.local))for(int p=0;p<PortCount;p++)
                    {
                        Send(c,query,c.broadcast,FirstPort+p);
                        if(!IPAddress.IsLoopback(c.local)&&BroadcastEnabled)Send(c,query,IPAddress.Broadcast,FirstPort+p);
                    }
            }
            // Work is distributed across frames and interfaces. No blocking network calls.
            for(int budget=0,idle=0;budget<96&&idle<channels.Count;)
            {
                var c=channels[nextChannel++%channels.Count];
                if(c.peers.Count==0||now<c.restart){idle++;continue;}
                idle=0;int cursor=c.cursor++;Send(c,query,c.peers[cursor/PortCount],FirstPort+cursor%PortCount);budget++;
                if(c.cursor>=c.peers.Count*PortCount){c.cursor=0;c.restart=now+2;}
            }
            foreach(var c in channels)for(int n=0;n<128;n++)try
            {
                if(c.socket.Available==0)break;
                var source=new IPEndPoint(IPAddress.Any,0);byte[] data=c.socket.Receive(ref source);
                if(data.Length>1024||data.Length<=Reply.Length)continue;
                string json=new UTF8Encoding(false,true).GetString(data);if(!json.StartsWith(Reply,StringComparison.Ordinal))continue;
                var room=JsonUtility.FromJson<DuelLanRoom>(json.Substring(Reply.Length));
                if(room==null||room.nonce!=nonce||!HexId(room.id)||room.title==null||room.title.Length>40||room.port<1||room.port>65535||room.max!=6||room.count<1||room.count>6)continue;
                if(room.protocol!=Protocol){if(incompatible.Count<64&&incompatible.Add(room.id)){Revision++;Debug.LogWarning("[LAN] Incompatible room protocol="+room.protocol+", local="+Protocol);}continue;}
                if(!DuelRoomCode.TryNormalize(room.code,out var code)||code!=room.code)continue;
                room.address=source.Address.ToString();room.seenAt=now;string key=room.id;
                if(!rooms.ContainsKey(key)&&rooms.Count>=64)continue;
                if(rooms.TryGetValue(key,out var old)&&old.address!=room.address)
                {
                    // Keep an already responding endpoint stable instead of swapping NICs every packet.
                    if(now-old.seenAt<3){room.address=old.address;room.seenAt=old.seenAt;continue;}
                }
                if(old==null||old.code!=room.code||old.title!=room.title||old.count!=room.count||old.port!=room.port||old.address!=room.address||old.joinable!=room.joinable||old.locked!=room.locked)
                {Revision++;if(old==null)Debug.Log("[LAN] Room discovered at "+room.address+":"+room.port);}
                rooms[key]=room;
            }catch(Exception e)when(e is SocketException||e is ArgumentException||e is DecoderFallbackException){ }
            var expired=new List<string>();foreach(var pair in rooms)if(now-pair.Value.seenAt>12)expired.Add(pair.Key);
            foreach(var key in expired){rooms.Remove(key);Revision++;}
        }
        public string SearchStatus(double now)
        {
            if(Rooms.Count>0)return $"{Rooms.Count}개의 방"+(IncompatibleCount>0?$" · 다른 버전 {IncompatibleCount}개 제외":"");
            if(IncompatibleCount>0)return "다른 버전의 방이 발견됐습니다. 모두 같은 최신 빌드로 실행하세요.";
            if(Error.Length>0)return Error;
            return now-startedAt<LookupDuration?"같은 LAN의 방을 찾는 중…":"방이 보이지 않으면 방장의 초대 코드를 받아 직접 참가하세요.";
        }
        public static bool HexId(string value)
        {if(value==null||value.Length!=32)return false;foreach(char c in value)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;return true;}
        public void StopHosting(){host?.Dispose();host=null;}
        public void StopBrowsing(){foreach(var c in channels)c.socket.Dispose();channels.Clear();}
        public void Dispose(){StopHosting();StopBrowsing();rooms.Clear();}
    }
}
