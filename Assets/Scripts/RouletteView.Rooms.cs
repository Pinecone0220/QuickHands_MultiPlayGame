using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement roomBrowser,roomRows,roomPasswordPanel;
        private TextField roomTitleInput,roomPasswordInput,roomJoinPassword;
        private Label roomSearchStatus,roomJoinTitle;
        private Button roomJoinButton;
        private DuelLanRoom selectedRoom;
        private int roomRevision=-1;
        private bool roomBrowserOpen;
        private void BuildRoomMenu(VisualElement panel)
        {
            roomTitleInput=new TextField("방 제목"){maxLength=40};roomPasswordInput=new TextField("비밀번호"){isPasswordField=true,maxLength=32};
            panel.Add(roomTitleInput);panel.Add(roomPasswordInput);var buttons=Box(panel,"button-row");
            Button(buttons,"방 만들기",()=>{manager.CreateRoom(roomTitleInput.value,roomPasswordInput.value);if(manager.Busy)roomPasswordInput.SetValueWithoutNotify("");},"secondary");
            Button(buttons,"방 목록보기",()=>SetRoomBrowser(true),"secondary");
            roomBrowser=Box(root,"weapon-info-layer");var box=Box(roomBrowser,"room-list-panel");
            var header=Box(box,"weapon-info-heading");Text(header,"방 목록","weapon-info-title");
            Button(header,"새로고침",()=>{manager.BrowseRooms();roomRevision=-1;},"secondary");Button(header,"닫기",()=>SetRoomBrowser(false),"secondary");
            roomSearchStatus=Text(box,"","fineprint");
            var scroll=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("room-list-scroll");box.Add(scroll);roomRows=Box(scroll,"room-list-rows");
            roomPasswordPanel=Box(box,"room-password-panel");roomJoinTitle=Text(roomPasswordPanel,"","room-join-title");roomJoinTitle.enableRichText=false;
            roomJoinPassword=new TextField("비밀번호"){isPasswordField=true,maxLength=32};roomPasswordPanel.Add(roomJoinPassword);
            roomJoinButton=Button(roomPasswordPanel,"참가",()=>{if(selectedRoom!=null){manager.JoinRoom(selectedRoom,roomJoinPassword.value);roomJoinPassword.SetValueWithoutNotify("");}},"primary");
            roomBrowser.RegisterCallback<ClickEvent>(e=>{if(e.target==roomBrowser)SetRoomBrowser(false);});
            Show(roomPasswordPanel,false);Show(roomBrowser,false);
        }
        private void SetRoomBrowser(bool visible)
        {
            roomBrowserOpen=visible;selectedRoom=null;roomJoinPassword.SetValueWithoutNotify("");Show(roomPasswordPanel,false);Show(roomBrowser,visible);
            if(visible){manager.BrowseRooms();roomRevision=-1;roomBrowser.BringToFront();}else manager.Rooms.StopBrowsing();
        }
        private void RefreshRoomBrowser()
        {
            if(!roomBrowserOpen)return;
            if(manager.Busy){SetRoomBrowser(false);return;}
            var rooms=manager.Rooms;roomSearchStatus.text=rooms.Error.Length>0?rooms.Error:rooms.Rooms.Count==0?"같은 Wi-Fi/LAN의 방을 찾는 중…":$"{rooms.Rooms.Count}개의 방";
            if(roomRevision==rooms.Revision)return;roomRevision=rooms.Revision;
            roomRows.Clear();var sorted=new List<DuelLanRoom>(rooms.Rooms);sorted.Sort((a,b)=>string.Compare(a.title,b.title,StringComparison.OrdinalIgnoreCase));
            bool selectionExists=false;
            foreach(var room in sorted)
            {
                var row=Box(roomRows,"room-list-row");var info=Box(row,"room-list-info");var title=Text(info,room.title,"room-list-title");title.enableRichText=false;
                Text(info,$"{room.count} / {room.max}  ·  {(room.locked?"비밀번호":"공개")}  ·  {(!room.joinable?room.count>=6?"가득 참":"게임 중":"대기 중")}","fineprint");
                var entry=room;var button=Button(row,room.locked?"선택":"참가",()=>
                {
                    if(!entry.locked){manager.JoinRoom(entry,"");return;}
                    selectedRoom=entry;roomJoinTitle.text=entry.title;roomJoinPassword.SetValueWithoutNotify("");Show(roomPasswordPanel,true);roomJoinPassword.Focus();
                },"secondary");button.SetEnabled(room.joinable&&room.count<6);
                if(selectedRoom!=null&&room.id==selectedRoom.id&&room.address==selectedRoom.address){selectedRoom=room;selectionExists=true;roomJoinButton.SetEnabled(room.joinable&&room.count<6);}
            }
            if(selectedRoom!=null&&!selectionExists){selectedRoom=null;Show(roomPasswordPanel,false);roomJoinPassword.SetValueWithoutNotify("");}
        }
    }
}
