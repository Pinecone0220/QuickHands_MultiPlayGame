using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement roomBrowser,roomRows,roomPasswordPanel;
        private TextField roomTitleInput,roomCodeInput,roomPasswordInput,roomJoinPassword,roomInviteInput,roomInvitePassword;
        private Label roomSearchStatus,roomJoinTitle,roomCodeLabel;
        private Button roomJoinButton,roomCreateButton,roomCodeJoinButton,roomBrowseButton,roomInviteCopy;
        private DuelLanRoom selectedRoom;
        private int roomRevision=-1;
        private bool roomBrowserOpen;
        private void BuildRoomMenu(VisualElement panel)
        {
            roomTitleInput=new TextField("방 제목"){maxLength=40};roomPasswordInput=new TextField("비밀번호"){isPasswordField=true,maxLength=32};
            roomCodeInput=new TextField("방 코드"){maxLength=DuelRoomCode.MaxLength,tooltip="영문·숫자 4~12자 · 대소문자를 구분하지 않습니다."};
            panel.Add(roomTitleInput);panel.Add(roomCodeInput);panel.Add(roomPasswordInput);var buttons=Box(panel,"button-row");
            roomCreateButton=Button(buttons,"방 만들기",()=>manager.CreateRoom(roomTitleInput.value,roomPasswordInput.value,roomCodeInput.value),"secondary");
            roomBrowseButton=Button(buttons,"방 목록보기",()=>SetRoomBrowser(true),"secondary");
            roomCodeJoinButton=Button(panel,"코드로 참가",()=>manager.JoinRoomCode(roomCodeInput.value,roomPasswordInput.value),"secondary");
            roomBrowser=Box(root,"weapon-info-layer");var box=Box(roomBrowser,"room-list-panel");
            var header=Box(box,"weapon-info-heading");Text(header,"방 목록","weapon-info-title");
            Button(header,"새로고침",()=>{manager.BrowseRooms();roomRevision=-1;},"secondary");Button(header,"닫기",()=>SetRoomBrowser(false),"secondary");
            roomSearchStatus=Text(box,"","fineprint");
            var scroll=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("room-list-scroll");box.Add(scroll);roomRows=Box(scroll,"room-list-rows");
            roomPasswordPanel=Box(box,"room-password-panel");roomJoinTitle=Text(roomPasswordPanel,"","room-join-title");roomJoinTitle.enableRichText=false;
            roomJoinPassword=new TextField("비밀번호"){isPasswordField=true,maxLength=32};roomPasswordPanel.Add(roomJoinPassword);
            roomJoinButton=Button(roomPasswordPanel,"참가",()=>{if(selectedRoom!=null){manager.JoinRoom(selectedRoom,roomJoinPassword.value);roomJoinPassword.SetValueWithoutNotify("");}},"primary");
            var fallback=Box(box,"room-password-panel");
            Text(fallback,"목록에 방이 보이지 않나요? 방장의 초대 코드를 받아 직접 참가하세요.","fineprint");
            roomInviteInput=new TextField("초대 코드"){maxLength=4096};roomInvitePassword=new TextField("비밀번호"){isPasswordField=true,maxLength=32};fallback.Add(roomInviteInput);fallback.Add(roomInvitePassword);
            var inviteStatus=Text(fallback,"","fineprint");
            Button(fallback,"초대 코드로 직접 참가",()=>{manager.JoinRoomInvite(roomInviteInput.value,roomInvitePassword.value);inviteStatus.text=manager.Status;roomInvitePassword.SetValueWithoutNotify("");},"secondary");
            roomBrowser.RegisterCallback<ClickEvent>(e=>{if(e.target==roomBrowser)SetRoomBrowser(false);});
            Show(roomPasswordPanel,false);Show(roomBrowser,false);
            roomCodeLabel=Text(root,"","fineprint");roomCodeLabel.pickingMode=PickingMode.Ignore;
            roomCodeLabel.style.position=Position.Absolute;roomCodeLabel.style.left=22;roomCodeLabel.style.top=76;
            Show(roomCodeLabel,false);
            roomInviteCopy=Button(root,"초대 코드 복사",()=>{string invite=manager.RoomInvite;if(invite.Length>0){UnityEngine.GUIUtility.systemCopyBuffer=invite;roomInviteCopy.text="복사 완료";}else roomInviteCopy.text="LAN 연결을 확인하세요";},"secondary");
            roomInviteCopy.style.position=Position.Absolute;roomInviteCopy.style.left=22;roomInviteCopy.style.top=122;
            Show(roomInviteCopy,false);
        }
        private void SetRoomBrowser(bool visible)
        {
            roomBrowserOpen=visible;selectedRoom=null;roomJoinPassword.SetValueWithoutNotify("");Show(roomPasswordPanel,false);Show(roomBrowser,visible);
            if(visible){manager.BrowseRooms();roomRevision=-1;roomBrowser.BringToFront();}else manager.Rooms.StopBrowsing();
        }
        private void RefreshRoomBrowser()
        {
            var player=HelloWorldPlayer.Local;
            bool lobby=manager.Busy&&manager.RoomCode.Length>0&&player!=null&&player.IsSpawned&&player.State.Value.Stage==DuelStage.Idle&&player.State.Value.Phase!=DuelPhase.Playing;
            Show(roomCodeLabel,lobby);roomCodeLabel.text="방 코드  "+manager.RoomCode;
            Show(roomInviteCopy,lobby&&manager.CanShareRoomInvite);
            if(!lobby)roomInviteCopy.text="초대 코드 복사";
            bool idle=!manager.Busy&&!manager.RoomRequestPending;
            roomTitleInput.SetEnabled(idle);roomCodeInput.SetEnabled(idle);roomPasswordInput.SetEnabled(idle);
            roomCreateButton.SetEnabled(idle);roomCodeJoinButton.SetEnabled(idle);roomBrowseButton.SetEnabled(idle);
            if(manager.Busy&&roomPasswordInput.value.Length>0)roomPasswordInput.SetValueWithoutNotify("");
            if(!roomBrowserOpen)return;
            if(manager.Busy){SetRoomBrowser(false);return;}
            var rooms=manager.Rooms;roomSearchStatus.text=rooms.SearchStatus(UnityEngine.Time.realtimeSinceStartupAsDouble);
            if(roomRevision==rooms.Revision)return;roomRevision=rooms.Revision;
            roomRows.Clear();var sorted=new List<DuelLanRoom>(rooms.Rooms);sorted.Sort((a,b)=>string.Compare(a.title,b.title,StringComparison.OrdinalIgnoreCase));
            bool selectionExists=false;
            foreach(var room in sorted)
            {
                var row=Box(roomRows,"room-list-row");var info=Box(row,"room-list-info");var title=Text(info,room.title,"room-list-title");title.enableRichText=false;
                Text(info,$"코드 {room.code}  ·  {room.count} / {room.max}  ·  {(room.locked?"비밀번호":"공개")}  ·  {(!room.joinable?room.count>=6?"가득 참":"게임 중":"대기 중")}","fineprint");
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
