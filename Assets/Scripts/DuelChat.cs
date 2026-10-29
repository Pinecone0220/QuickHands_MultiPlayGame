using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace HelloWorld
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class DuelChat:MonoBehaviour
    {
        private static DuelChat current;
        private static int inputFrame=-1;
        public static bool BlocksGameplay=>(current!=null&&current.open)||inputFrame==Time.frameCount;
        public bool Opened=>open;
        public int MessageCount=>lines.Count;
        private VisualElement root,layer,panel;
        private ScrollView history;
        private TextField input;
        private TextElement inputEditor;
        private Label hint,empty;
        private readonly List<DuelChatLine> lines=new List<DuelChatLine>();
        private readonly List<Label> labels=new List<Label>();
        private Keyboard keyboard,imeKeyboard;
        private bool open,hadSession,noticeShown,composing,awaitingEcho,pendingSubmit,inputFocused;
        private int submitFrame=-1,compositionEndedFrame=-1,pointerClosedFrame=-1,lastEditFrame=-1;
        private double lastSend=-10,hintUntil,lastEditAt;
        private string submitted="",draft="";
        private bool Available
        {
            get{var p=HelloWorldPlayer.Local;return p!=null&&p.IsSpawned&&p.Seat.Value>=0;}
        }
        private void Awake()
        {
            current=this;inputFrame=-1;
            root=GetComponent<UIDocument>().rootVisualElement;
            BuildSpeech();
            layer=new VisualElement{name="room-chat",pickingMode=PickingMode.Ignore};layer.AddToClassList("chat-layer");root.Add(layer);
            panel=new VisualElement();panel.AddToClassList("chat-panel");layer.Add(panel);
            var title=new Label("채팅");title.AddToClassList("chat-title");panel.Add(title);
            history=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            history.AddToClassList("chat-log");history.contentViewport.style.overflow=Overflow.Hidden;panel.Add(history);
            empty=new Label("아직 대화가 없습니다.");empty.AddToClassList("chat-row");history.Add(empty);
            CreateInput();
            hint=new Label();hint.AddToClassList("chat-hint");panel.Add(hint);
            root.RegisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnPointer,TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(OnWheel,TrickleDown.TrickleDown);
            SetOpen(false);layer.style.display=DisplayStyle.None;
        }
        private void Update()
        {
            if(keyboard!=Keyboard.current)
            {
                if(keyboard!=null)keyboard.onIMECompositionChange-=Composition;
                keyboard=Keyboard.current;composing=false;
                if(keyboard!=null)keyboard.onIMECompositionChange+=Composition;
            }
            bool available=Available;
            if(!available)
            {
                if(hadSession)ClearRoom();hadSession=false;layer.style.display=DisplayStyle.None;return;
            }
            hadSession=true;layer.style.display=DisplayStyle.Flex;
            if(DuelSettings.WindowOpen)return;
            if(open&&keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)Close();
            else if(inputFrame!=Time.frameCount&&keyboard!=null&&
                (keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                inputFrame=Time.frameCount;
                if(!open)Open();
                else {pendingSubmit=true;submitFrame=Time.frameCount;Edited();}
            }
            if(open&&Mouse.current!=null&&(Mouse.current.leftButton.wasPressedThisFrame||Mouse.current.rightButton.wasPressedThisFrame||Mouse.current.middleButton.wasPressedThisFrame))
            {
                var mouse=Mouse.current.position.ReadValue();mouse.y=Screen.height-mouse.y;
                if(!DuelPointer.UiMode||!panel.worldBound.Contains(RuntimePanelUtils.ScreenToPanel(root.panel,mouse)))
                {Close();pointerClosedFrame=Time.frameCount;}
            }
            if(open&&DuelPointer.UiMode&&Mouse.current!=null)
            {
                var mouse=Mouse.current.position.ReadValue();mouse.y=Screen.height-mouse.y;
                float wheel=Mouse.current.scroll.ReadValue().y;
                if(wheel!=0&&!history.worldBound.Contains(RuntimePanelUtils.ScreenToPanel(root.panel,mouse)))
                    history.scrollOffset=new Vector2(0,Mathf.Clamp(history.scrollOffset.y-wheel*.35f,0,history.verticalScroller.highValue));
            }
            // Process cancellation before sending. Keep Enter while IME commits its final text.
            if(pendingSubmit&&!composing&&Time.frameCount>submitFrame+1&&
                Time.frameCount>lastEditFrame+1&&Time.frameCount>compositionEndedFrame+1&&
                Time.realtimeSinceStartupAsDouble-lastEditAt>=.05&&Time.realtimeSinceStartupAsDouble-lastSend>=.75)
            {
                pendingSubmit=false;
                if(open)Submit();
            }
            if(Time.realtimeSinceStartupAsDouble>=hintUntil)hint.text=open?
                pendingSubmit?"전송 대기 중 · Esc 취소":"Enter 전송 · Esc 닫기 · 기록 스크롤":"Enter로 채팅";
        }
        private void LateUpdate()
        {
            UpdateSpeech();
            if(!Available)return;
            layer.BringToFront();
        }
        private void Composition(IMECompositionString text)
        {
            // Native cancellation can arrive after Close. It belongs to the old editor.
            if(!open)return;
            if(composing&&text.Count==0)compositionEndedFrame=Time.frameCount;
            composing=text.Count>0;
            Edited();
        }
        private void Edited(){lastEditFrame=Time.frameCount;lastEditAt=Time.realtimeSinceStartupAsDouble;}
        private void CreateInput()
        {
            if(input!=null)
            {
                inputEditor.Blur();
                inputEditor.UnregisterCallback<FocusEvent>(OnInputFocus);
                inputEditor.UnregisterCallback<BlurEvent>(OnInputBlur);
                input.RemoveFromHierarchy();
            }
            // A new field gets a new TextEditingUtilities/IME preview and saved cursor.
            // Clearing and reusing a focused field retains invalid native preview indices.
            inputFocused=false;
            input=new TextField{maxLength=240,multiline=false,value=draft};input.AddToClassList("chat-input");
            panel.Add(input);
            if(hint!=null)input.PlaceBehind(hint);
            inputEditor=input.Q<VisualElement>(TextField.textInputUssName).Q<TextElement>();
            input.textSelection.selectAllOnFocus=false;input.textSelection.selectAllOnMouseUp=false;
            // This buffer is freshly constructed, before focus/IME preview can begin.
            if(draft.Length>0)input.cursorIndex=input.selectIndex=draft.Length;
            inputEditor.RegisterCallback<FocusEvent>(OnInputFocus);
            inputEditor.RegisterCallback<BlurEvent>(OnInputBlur);
            input.RegisterValueChangedCallback(e=>Edited());
        }
        private void OnInputFocus(FocusEvent e)
        {
            inputFocused=true;
            if(!open)return;
            var next=Keyboard.current;
            if(imeKeyboard!=null&&imeKeyboard!=next)EndIme();
            imeKeyboard=next;
            if(imeKeyboard!=null&&imeKeyboard.added)imeKeyboard.SetIMEEnabled(true);
        }
        private void OnInputBlur(BlurEvent e){inputFocused=false;}
        private void EndIme()
        {
            if(imeKeyboard!=null&&imeKeyboard.added)imeKeyboard.SetIMEEnabled(false);
            imeKeyboard=null;
        }
        private void OnKey(KeyDownEvent e)
        {
            if(!Available)return;
            bool enter=e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter;
            // The physical Enter (no character) must reach the editor so it can flush IME.
            // Only its separate CR/LF character would leave editing and must be swallowed.
            // A committed Unicode character is text, even if the native event carries Return.
            if(e.character=='\r'||e.character=='\n'||enter&&e.character=='\0'&&(!open||!Inside(input,e.target as VisualElement)))
            {
                e.StopImmediatePropagation();
            }
            else if((open||inputFrame==Time.frameCount)&&e.keyCode==KeyCode.Escape)e.StopImmediatePropagation();
            else if(open&&e.keyCode==KeyCode.Tab)e.StopImmediatePropagation();
        }
        private void OnPointer(PointerDownEvent e)
        {
            if((open||pointerClosedFrame==Time.frameCount)&&!Inside(panel,e.target as VisualElement))
            {if(open)Close();pointerClosedFrame=Time.frameCount;e.StopImmediatePropagation();}
        }
        private static bool Inside(VisualElement parent,VisualElement child)=>parent==child||parent.Contains(child);
        private void OnWheel(WheelEvent e)
        {if(open&&(!DuelPointer.UiMode||!Inside(history,e.target as VisualElement)))e.StopImmediatePropagation();}
        public void Open()
        {
            if(!Available)return;
            if(open){FocusInput();return;}
            composing=pendingSubmit=false;submitFrame=compositionEndedFrame=-1;
            CreateInput();
            SetOpen(true);inputFrame=Time.frameCount;
            layer.style.display=DisplayStyle.Flex;layer.BringToFront();
            FocusInput();input.schedule.Execute(FocusInput);
        }
        private void FocusInput()
        {
            if(!open||inputFocused)return;
            // Focus the editable leaf directly, including after display/layout changes.
            // The displayed value may contain an IME preview beyond the committed buffer.
            // Reusing that length as a cursor index corrupts Unity's composition state.
            inputEditor.Focus();
        }
        public void Close()
        {
            if(open){inputFrame=Time.frameCount;draft=input.text;}
            open=false; // Ignore late native composition events from the closing field.
            inputEditor.Blur();EndIme();SetOpen(false);composing=pendingSubmit=false;Bottom();
        }
        private void SetOpen(bool value)
        {
            open=value;panel.EnableInClassList("chat-active",open);
            input.style.display=open?DisplayStyle.Flex:DisplayStyle.None;
            history.verticalScrollerVisibility=open?ScrollerVisibility.Auto:ScrollerVisibility.Hidden;
            Picking(panel,open);
            hint.text=open?"Enter 전송 · Esc 닫기 · 기록 스크롤":"Enter로 채팅";
        }
        private static void Picking(VisualElement element,bool enabled)
        {
            element.pickingMode=enabled?PickingMode.Position:PickingMode.Ignore;
            for(int i=0;i<element.hierarchy.childCount;i++)Picking(element.hierarchy[i],enabled);
        }
        public void Submit()
        {
            if(!open||!Available||composing||compositionEndedFrame==Time.frameCount)return;
            string message=DuelChatLog.Normalize(input.text);
            if(message.Length==0){Close();draft="";return;}
            double now=Time.realtimeSinceStartupAsDouble;
            if(now-lastSend<.75){Hint("잠시 후 다시 보내세요.");return;}
            submitted=message;awaitingEcho=true;lastSend=now;
            // Blur first: Unity must restore its saved IME cursor before the buffer shrinks.
            Close();draft="";
            if(!HelloWorldPlayer.Local.SendChat(message))
            {draft=message;awaitingEcho=false;Open();}
        }
        public void Rejected()
        {
            if(awaitingEcho&&draft.Length==0&&(!open||input.text.Length==0))
            {
                bool resume=open;if(resume)Close();draft=submitted;if(resume)Open();
            }
            awaitingEcho=false;Hint("전송되지 않았습니다. 잠시 후 다시 보내세요.");
        }
        private void Hint(string text){hint.text=text;hintUntil=Time.realtimeSinceStartupAsDouble+3;}
        public void AddEntryNotice()
        {
            if(noticeShown)return;noticeShown=true;
            const string message="Alt를 눌러 마우스를 조작할 수 있습니다.\n마우스 휠이나 숫자키로 아이템과 무기를 교체할 수 있습니다.";
            lines.Add(new DuelChatLine{Sequence=0,Message=new Unity.Collections.FixedString512Bytes(message)});
            var label=new Label("[공지] "+message){enableRichText=false,pickingMode=open?PickingMode.Position:PickingMode.Ignore};
            label.AddToClassList("chat-row");label.AddToClassList("chat-notice");
            labels.Add(label);history.contentContainer.Add(label);empty.style.display=DisplayStyle.None;
            if(lines.Count>DuelChatLog.MaxLines){lines.RemoveAt(0);labels[0].RemoveFromHierarchy();labels.RemoveAt(0);}
            Bottom();
        }
        public void Receive(DuelChatLine line)
        {
            if(line.Sequence<=0)return;
            int index=lines.FindIndex(x=>x.Sequence>=line.Sequence);
            if(index>=0&&lines[index].Sequence==line.Sequence)return;
            int oldest=lines.FindIndex(x=>x.Sequence>0);
            if(lines.Count>=DuelChatLog.MaxLines&&oldest>=0&&line.Sequence<lines[oldest].Sequence)return;
            // Late history may arrive after a live message; never float an older record again.
            int newest=0;foreach(var previous in lines)if(previous.Sequence>newest)newest=previous.Sequence;
            if(line.Sequence>newest)PushSpeech(line);
            bool bottom=!open||history.verticalScroller.highValue-history.verticalScroller.value<5;
            if(index<0)index=lines.Count;
            lines.Insert(index,line);
            var label=new Label(line.Name+": "+line.Message){enableRichText=false,pickingMode=open?PickingMode.Position:PickingMode.Ignore};
            label.AddToClassList("chat-row");
            var local=HelloWorldPlayer.Local;
            if(local!=null&&local.IsSpawned&&line.Sender==local.OwnerClientId)
            {label.AddToClassList("chat-own");if(line.Message.ToString()==submitted)awaitingEcho=false;}
            labels.Insert(index,label);history.contentContainer.Insert(index,label);empty.style.display=DisplayStyle.None;
            if(lines.Count>DuelChatLog.MaxLines)
            {
                int remove=lines[0].Sequence==0?1:0;
                float removed=labels[remove].layout.height;lines.RemoveAt(remove);labels[remove].RemoveFromHierarchy();labels.RemoveAt(remove);
                if(!bottom)history.scrollOffset=new Vector2(0,Mathf.Max(0,history.scrollOffset.y-removed));
            }
            if(bottom)Bottom();
        }
        private void Bottom()
        {history.schedule.Execute(()=>{if(labels.Count>0)history.ScrollTo(labels[labels.Count-1]);});}
        public void ClearRoom()
        {
            hadSession=noticeShown=false;
            Close();lines.Clear();labels.Clear();history.Clear();history.Add(empty);empty.style.display=DisplayStyle.Flex;
            ClearSpeech();
            draft="";submitted="";awaitingEcho=false;lastSend=-10;hintUntil=0;composing=false;
            layer.style.display=DisplayStyle.None;
        }
        private void OnDestroy()
        {
            EndIme();
            if(keyboard!=null)keyboard.onIMECompositionChange-=Composition;
            root?.UnregisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerDownEvent>(OnPointer,TrickleDown.TrickleDown);
            root?.UnregisterCallback<WheelEvent>(OnWheel,TrickleDown.TrickleDown);
            layer?.RemoveFromHierarchy();if(current==this)current=null;
            speechLayer?.RemoveFromHierarchy();
        }
    }
}
