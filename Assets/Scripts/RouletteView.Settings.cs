using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private Button settingsButton,settingsClose;
        private VisualElement settingsLayer;
        private VisualElement goldDisplay;
        private Label goldDisplayAmount;
        private Texture2D goldCoinIcon;
        private bool otherWindowLastFrame,settingsEscapeHeld;
        private VisualElement attendanceLayer,attendanceDays;
        private Label attendanceTitle,attendanceStatus;
        private Button attendanceClaim,attendanceClose;
        private AudioSource attendanceAudio;
        private AudioClip attendanceOpenSound,attendanceRewardSound;
        private bool attendanceOpen,attendanceFromSettings,attendanceAutoChecked;
        private void BuildSettings()
        {
            settingsButton=Button(root,"설정",()=>SetSettingsOpen(true),"secondary");settingsButton.AddToClassList("settings-button");
            goldDisplay=Box(root,"gold-display");goldDisplay.pickingMode=PickingMode.Ignore;
            if(goldCoinIcon==null)goldCoinIcon=MakeGoldCoinIcon();var coin=new Image{image=goldCoinIcon,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};coin.AddToClassList("gold-display-icon");goldDisplay.Add(coin);
            goldDisplayAmount=Text(goldDisplay,"","gold-display-amount");goldDisplayAmount.pickingMode=PickingMode.Ignore;
            settingsLayer=Box(root,"settings-layer");
            var panel=Box(settingsLayer,"settings-panel");var header=Box(panel,"settings-header");
            Text(header,"설정","settings-title");settingsClose=Button(header,"닫기",()=>SetSettingsOpen(false),"secondary");
            AddSettingsSlider(panel,"배경음",0,1,DuelSettings.Music,value=>DuelSettings.Music=value);
            AddSettingsSlider(panel,"효과음",0,1,DuelSettings.Effects,value=>DuelSettings.Effects=value);
            AddSettingsSlider(panel,"마우스 감도",.25f,2.5f,DuelSettings.Sensitivity,value=>DuelSettings.Sensitivity=value);
            Text(panel,"변경 사항은 자동으로 저장됩니다.","settings-hint");
            Button(panel,"출석부",()=>SetAttendanceOpen(true,true),"secondary").AddToClassList("attendance-settings-button");
            Show(settingsLayer,false);BuildAttendance();
        }
        private void AddSettingsSlider(VisualElement panel,string name,float min,float max,float current,System.Action<float> save)
        {
            var block=Box(panel,"settings-control");var heading=Box(block,"settings-control-heading");
            Text(heading,name,"settings-label");var valueLabel=Text(heading,Mathf.RoundToInt(current*100)+"%","settings-value");
            var slider=new Slider(min,max){value=current};slider.AddToClassList("settings-slider");block.Add(slider);
            slider.RegisterValueChangedCallback(e=>{float value=Mathf.Round(e.newValue*100)/100;valueLabel.text=Mathf.RoundToInt(value*100)+"%";save(value);});
        }
        private bool SettingsAvailable()
        {
            if(!manager.Busy)return true;
            var p=HelloWorldPlayer.Local;
            return p!=null&&p.IsSpawned&&p.Seat.Value>=0&&p.State.Value.Stage==DuelStage.Idle&&p.State.Value.Phase==DuelPhase.Waiting;
        }
        private void SetSettingsOpen(bool visible)
        {
            if(visible&&!SettingsAvailable())return;
            DuelSettings.WindowOpen=visible;Show(settingsLayer,visible);
            if(visible){settingsLayer.BringToFront();settingsClose.Focus();}
            else if(SettingsAvailable())settingsButton.Focus();
            DuelPointer.ApplyCursor();
        }
        private void UpdateSettings()
        {
            bool other=patchNotesOpen||weaponInfoOpen||quitConfirmOpen||gachaOpen||attendanceOpen||DuelChat.BlocksGameplay;
            if(attendanceOpen&&!SettingsAvailable())SetAttendanceOpen(false,false);
            if(DuelSettings.WindowOpen&&!SettingsAvailable())SetSettingsOpen(false);
            bool escapeDown=Keyboard.current!=null&&Keyboard.current.escapeKey.isPressed;
            bool escapePressed=escapeDown&&!settingsEscapeHeld;settingsEscapeHeld=escapeDown;
            if(escapePressed)
            {
                if(attendanceOpen)SetAttendanceOpen(false,false);
                else if(DuelSettings.WindowOpen)SetSettingsOpen(false);
                else if(SettingsAvailable()&&!other&&!otherWindowLastFrame)SetSettingsOpen(true);
            }
            otherWindowLastFrame=other;
        }
        private void UpdateSettingsLayer()
        {
            bool visible=SettingsAvailable()&&!patchNotesOpen&&!weaponInfoOpen&&!quitConfirmOpen&&!gachaOpen&&!attendanceOpen;
            Show(settingsButton,visible);
            Show(goldDisplay,visible);if(visible)goldDisplayAmount.text=GachaGold.ToString("N0")+"원";
            if(attendanceOpen)attendanceLayer.BringToFront();
            else if(DuelSettings.WindowOpen)settingsLayer.BringToFront();
            else if(visible){goldDisplay.BringToFront();settingsButton.BringToFront();}
        }
        private static string AttendanceKey(System.DateTime date)=>"qh.attendance.claim."+date.ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture);
        private static string AttendanceShownKey(System.DateTime date)=>"qh.attendance.shown."+date.ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture);
        private void BuildAttendance()
        {
            attendanceLayer=Box(root,"attendance-layer");var panel=Box(attendanceLayer,"attendance-panel");
            var heading=Box(panel,"settings-header");attendanceTitle=Text(heading,"출석부","settings-title");
            attendanceClose=Button(heading,"닫기",()=>SetAttendanceOpen(false,false),"secondary");
            Text(panel,"매일 출석 보상 · 골드 100원","attendance-description");
            attendanceDays=Box(panel,"attendance-days");attendanceStatus=Text(panel,"","attendance-status");
            attendanceClaim=Button(panel,"보상 받기",ClaimAttendance,"primary");
            attendanceAudio=gameObject.AddComponent<AudioSource>();attendanceAudio.playOnAwake=false;attendanceAudio.spatialBlend=0;
            attendanceOpenSound=MakeAttendanceSound(false);attendanceRewardSound=MakeAttendanceSound(true);Show(attendanceLayer,false);
        }
        private void SetAttendanceOpen(bool visible,bool fromSettings)
        {
            if(visible&&!SettingsAvailable())return;
            if(visible)
            {
                attendanceFromSettings=fromSettings;
                if(DuelSettings.WindowOpen)SetSettingsOpen(false);
                attendanceOpen=true;DuelSettings.WindowOpen=true;RefreshAttendance();Show(attendanceLayer,true);attendanceLayer.BringToFront();attendanceClose.Focus();
                attendanceAudio.PlayOneShot(attendanceOpenSound,.55f*DuelSettings.Effects);
            }
            else
            {
                bool restore=attendanceFromSettings&&SettingsAvailable();attendanceOpen=false;attendanceFromSettings=false;Show(attendanceLayer,false);DuelSettings.WindowOpen=false;
                if(restore)SetSettingsOpen(true);else DuelPointer.ApplyCursor();
            }
            DuelPointer.ApplyCursor();
        }
        private void UpdateAttendance()
        {
            if(attendanceAutoChecked||manager.Busy||patchNotesOpen||weaponInfoOpen||quitConfirmOpen||gachaOpen||DuelSettings.WindowOpen)return;
            attendanceAutoChecked=true;var today=System.DateTime.Now.Date;string key=AttendanceShownKey(today);
            if(PlayerPrefs.GetInt(key,0)!=0)return;
            PlayerPrefs.SetInt(key,1);PlayerPrefs.Save();SetAttendanceOpen(true,false);
        }
        private void RefreshAttendance()
        {
            var today=System.DateTime.Now.Date;attendanceTitle.text=today.ToString("yyyy년 M월",System.Globalization.CultureInfo.InvariantCulture)+" 출석부";
            attendanceDays.Clear();var first=new System.DateTime(today.Year,today.Month,1);int count=System.DateTime.DaysInMonth(today.Year,today.Month);
            var header=Box(attendanceDays,"attendance-week");foreach(string weekday in new[]{"일","월","화","수","목","금","토"})Text(header,weekday,"attendance-weekday");
            VisualElement week=null;int offset=(int)first.DayOfWeek;
            for(int index=0;index<offset+count;index++)
            {
                if(index%7==0)week=Box(attendanceDays,"attendance-week");
                int day=index-offset+1;if(day<1){Box(week,"attendance-empty");continue;}
                var date=first.AddDays(day-1);bool claimed=PlayerPrefs.GetInt(AttendanceKey(date),0)!=0;
                var cell=Box(week,"attendance-day");if(date==today)cell.AddToClassList("today");if(claimed)cell.AddToClassList("claimed");
                var dayHeader=Box(cell,"attendance-day-header");Text(dayHeader,day.ToString(),"attendance-day-number");
                if(claimed)Text(dayHeader,"받음","attendance-day-claimed");
                var reward=Box(cell,"attendance-day-reward");
                var icon=new Image{image=goldCoinIcon,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};icon.AddToClassList("attendance-gold-icon");reward.Add(icon);
                Text(reward,"100원","attendance-day-state");
            }
            int remainder=(offset+count)%7;if(remainder!=0)for(int i=remainder;i<7;i++)Box(week,"attendance-empty");
            bool done=PlayerPrefs.GetInt(AttendanceKey(today),0)!=0;
            attendanceStatus.text=done?"오늘의 출석 보상을 받았습니다.":"오늘 출석하고 골드 100원을 받으세요.";
            attendanceClaim.text=done?"수령 완료":"보상 받기";attendanceClaim.SetEnabled(!done);
        }
        private void ClaimAttendance()
        {
            var today=System.DateTime.Now.Date;string key=AttendanceKey(today);
            if(PlayerPrefs.GetInt(key,0)!=0){RefreshAttendance();return;}
            if(GachaGold>int.MaxValue-100){attendanceStatus.text="골드가 최대 보유량에 도달했습니다.";return;}
            PlayerPrefs.SetInt(GoldKey,GachaGold+100);PlayerPrefs.SetInt(key,1);PlayerPrefs.Save();
            UpdateGachaGold();RefreshAttendance();attendanceAudio.PlayOneShot(attendanceRewardSound,.7f*DuelSettings.Effects);
        }
        private static AudioClip MakeAttendanceSound(bool reward)
        {
            const int rate=22050;float duration=reward?.55f:.22f;var data=new float[(int)(rate*duration)];
            float[] tones=reward?new[]{523.25f,659.25f,783.99f}:new[]{440f,587.33f};
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,value=0;
                for(int j=0;j<tones.Length;j++){float age=t-j*(reward?.09f:.045f);if(age<0)continue;float envelope=Mathf.Exp(-age*(reward?7f:15f))*(1-Mathf.Exp(-age*100));value+=envelope*(Mathf.Sin(2*Mathf.PI*tones[j]*age)+.18f*Mathf.Sin(4*Mathf.PI*tones[j]*age))*.16f;}
                data[i]=Mathf.Clamp(value,-.9f,.9f);
            }
            var clip=AudioClip.Create(reward?"Attendance reward":"Attendance open",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private static Texture2D MakeGoldCoinIcon()
        {
            const int size=64,samples=3;var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Color pixel=Color.clear;
                for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
                {
                    float px=(x+(sx+.5f)/samples-size*.5f)/(size*.5f),py=(y+(sy+.5f)/samples-size*.5f)/(size*.5f);
                    float radius=Mathf.Sqrt(px*px+py*py);if(radius>.94f)continue;
                    Color color=radius>.87f?new Color(.36f,.21f,.07f):radius>.76f?new Color(.78f,.55f,.20f):
                        Color.Lerp(new Color(.53f,.32f,.09f),new Color(1f,.82f,.43f),Mathf.Clamp01(.55f+py*.35f-px*.18f));
                    if(radius>.67f&&radius<.71f)color=new Color(.43f,.27f,.09f);
                    // Four-point star and crossed inlays echo the table's brass ornament.
                    float star=Mathf.Abs(px)*1.15f+Mathf.Abs(py)*1.15f;
                    if(star<.32f&&(Mathf.Abs(px)<.075f||Mathf.Abs(py)<.075f||star<.16f))color=new Color(1f,.91f,.61f);
                    if(radius>.50f&&radius<.56f&&Mathf.Abs(Mathf.Sin(Mathf.Atan2(py,px)*6))<.14f)color=new Color(.95f,.75f,.34f);
                    pixel+=color/(samples*samples);
                }
                pixels[y*size+x]=pixel;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Quick Hands gold coin",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
    }
}
