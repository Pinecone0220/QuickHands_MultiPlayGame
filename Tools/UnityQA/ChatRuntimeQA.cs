using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HelloWorld;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ChatRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    HelloWorldManager manager;DuelChat chat;RouletteView view;UIDocument document;
    string role,path;int checks;double deadline;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--chat-qa");if(i<0)return;
        var qa=new GameObject("Chat QA").AddComponent<ChatRuntimeQA>();qa.role=args[i+1];
        Application.targetFrameRate=30;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        i=Array.IndexOf(args,"--chat-dir");qa.path=i<0?Path.Combine(Application.dataPath,"../ChatSync"):args[i+1];Directory.CreateDirectory(qa.path);
    }
    T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,F).GetValue(target);
    List<DuelChatLine> Lines=>Field<List<DuelChatLine>>(chat,"lines");
    void Check(bool ok,string reason){checks++;if(!ok){Finish("FAIL chat "+role+": "+reason,1);throw new Exception(reason);}}
    void Mark(string key)=>File.WriteAllText(Path.Combine(path,role+"-"+key),"ok");
    bool Seen(string who,string key)=>File.Exists(Path.Combine(path,who+"-"+key));
    bool Has(string message)=>Lines.Exists(x=>x.Message.ToString()==message);
    void Finish(string message,int code=0)
    {
        Debug.Log(message);File.WriteAllText(Path.Combine(path,role+"-done"),message);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    IEnumerator KeyPress(Key key)
    {
        if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key));yield return null;yield return null;
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());yield return null;yield return null;
    }
    IEnumerator OutsideClick()
    {
        if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
        var state=new MouseState{position=new Vector2(Screen.width*.9f,Screen.height*.8f)};
        InputSystem.QueueStateEvent(Mouse.current,state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));yield return null;yield return null;
        InputSystem.QueueStateEvent(Mouse.current,state);yield return null;yield return null;
    }
    IEnumerator Start()
    {
        deadline=Time.realtimeSinceStartupAsDouble+150;yield return new WaitForSecondsRealtime(1);
        manager=HelloWorldManager.Instance;chat=manager.GetComponent<DuelChat>();view=manager.GetComponent<RouletteView>();document=FindFirstObjectByType<UIDocument>();
        if(role=="speech")yield return Speech();else if(role=="solo")yield return Solo();else if(role=="focus")yield return FocusInput();else if(role=="fresh-ime"){yield return FreshIme();yield return ImeSubmit();yield return FocusInput();yield return Solo();}else if(role=="ime-submit"){yield return ImeSubmit();yield return FocusInput();yield return Solo();}else if(role=="text-start"){yield return TextStart();yield return FocusInput();yield return Solo();}else yield return Peers();
        Finish("PASS chat "+role+": "+checks+" checks");
    }
    IEnumerator TypeText(string text)
    {
        // UI Toolkit's text provider reads native IMGUI events, not QueueTextEvent.
        // Route equivalent text key events to the current leaf; never focus or set the value here.
        var controller=document.rootVisualElement.focusController;
        var target=(VisualElement)typeof(FocusController).GetMethod("GetLeafFocusedElement",F).Invoke(controller,null);
        foreach(char c in text)
        {using(var e=KeyDownEvent.GetPooled(new Event{type=EventType.KeyDown,character=c}))target.SendEvent(e);}
        yield return null;yield return null;
    }
    IEnumerator FocusInput()
    {
        var button=Field<Button>(view,"weaponInfoButton");int clicks=0;button.clicked+=()=>clicks++;
        button.Focus();yield return null;
        using(var e=NavigationSubmitEvent.GetPooled())button.SendEvent(e);
        yield return null;yield return KeyPress(Key.Enter);
        Check(clicks==0&&!Field<bool>(view,"weaponInfoOpen"),"Enter/navigation submit cannot activate a menu button");
        manager.Difficulty=AiDifficulty.Easy;manager.Begin(DuelMode.AI,false,"127.0.0.1","17951");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        yield return null;yield return null;
        var ready=Field<Button>(view,"rematch");ready.clicked+=()=>clicks++;ready.Focus();yield return null;
        yield return KeyPress(Key.Enter);
        Check(chat.Opened&&clicks==0,"Enter opens chat without activating the previously focused ready button");
        var input=Field<TextField>(chat,"input");var editor=input.Q<VisualElement>(TextField.textInputUssName).Q<TextElement>();
        var focused=typeof(FocusController).GetMethod("GetLeafFocusedElement",F).Invoke(input.focusController,null);
        Check(focused==editor,"editable text leaf receives focus after opening");
        yield return TypeText("바로 입력 123");
        Check(input.value=="바로 입력 123","real text events type immediately without a mouse click");
        Check(input.cursorIndex==input.value.Length&&input.selectIndex==input.value.Length,"caret follows typed text");
        using(var e=NavigationSubmitEvent.GetPooled())ready.SendEvent(e);
        yield return null;yield return TypeText(" 계속");
        Check(clicks==0&&input.value=="바로 입력 123 계속","submit navigation cannot steal chat edit focus");
        yield return KeyPress(Key.Enter);yield return new WaitForSecondsRealtime(.2f);
        Check(!chat.Opened&&Has("바로 입력 123 계속")&&chat.MessageCount==1&&clicks==0,"Enter sends once without clicking UI");
        yield return KeyPress(Key.NumpadEnter);yield return TypeText("재입력");
        input=Field<TextField>(chat,"input");
        Check(chat.Opened&&input.value=="재입력","numpad Enter also restores immediate text editing");
        yield return KeyPress(Key.Escape);
        Check(!chat.Opened&&clicks==0,"Escape closes without activating UI");
        yield return KeyPress(Key.Enter);yield return TypeText(" 추가");
        input=Field<TextField>(chat,"input");
        Check(input.value=="재입력 추가","reopening keeps draft and appends at the caret");
        chat.Close();manager.Leave();while(HelloWorldPlayer.Local!=null)yield return null;
    }
    VisualElement TextTarget()=> (VisualElement)typeof(FocusController).GetMethod("GetLeafFocusedElement",F).Invoke(document.rootVisualElement.focusController,null);
    void TextKey(char c,KeyCode code=KeyCode.None)
    {
        var target=TextTarget()??document.rootVisualElement;
        using(var e=KeyDownEvent.GetPooled(new Event{type=EventType.KeyDown,keyCode=code,character=c}))target.SendEvent(e);
    }
    void QueueComposition(string text)
    {
        var e=IMECompositionEvent.Create(Keyboard.current.deviceId,text,-1);InputSystem.QueueEvent(ref e);
    }
    IEnumerator Speech()
    {
        var q=new DuelSpeechQueue(0);q.Add("하나",0);q.Advance(2.99);
        Check(q.Lines.Count==1&&q.Lines[0].Opacity==1,"message remains fully visible before three seconds");
        q.Advance(3);Check(q.Lines[0].Opacity==1,"message remains fully visible at exactly three seconds");
        q.Advance(3.5);Check(Mathf.Approximately(q.Lines[0].Opacity,.5f),"one-second fade starts after three-second hold");
        q.Advance(4);Check(q.Lines.Count==0,"message expires after hold plus fade");
        q=new DuelSpeechQueue(0);q.Add("첫째",0);q.Add("둘째",1);q.Advance(3.5);
        Check(q.Lines.Count==2&&Mathf.Approximately(q.Lines[0].Opacity,.5f)&&q.Lines[1].Opacity==1,"two messages have independent hold and fade times");
        q.Advance(4);Check(q.Lines.Count==1&&q.Lines[0].Text=="둘째"&&q.Lines[0].Opacity==1,"younger message stays fully visible for its own three seconds");
        q=new DuelSpeechQueue(0);q.Add("1",0);q.Add("2",0);q.Add("3",0);q.Advance(3);
        Check(q.Lines[0].Opacity==1&&q.Lines[1].Opacity==1&&q.Lines[2].Opacity==1,"all three messages keep their initial three-second hold");
        q.Advance(3.5);
        Check(Mathf.Approximately(q.Lines[0].Opacity,.5f)&&q.Lines[1].Opacity==1&&q.Lines[2].Opacity==1,"three messages fade oldest only");
        q.Advance(4);Check(q.Lines.Count==2&&q.Lines[0].Text=="2"&&q.Lines[0].Opacity==1,"next queued message starts after oldest disappears");
        q.Advance(4.5);Check(Mathf.Approximately(q.Lines[0].Opacity,.5f)&&q.Lines[1].Opacity==1,"sequential fading continues after queue drops to two");
        q.Advance(5);Check(q.Lines.Count==1&&q.Lines[0].Text=="3"&&q.Lines[0].Opacity==1,"last message waits its turn");
        q.Advance(6);Check(q.Lines.Count==0,"three-message queue fully expires");
        q.Add("평소",7);q.Add("복귀",8);q.Advance(10.5);
        Check(Mathf.Approximately(q.Lines[0].Opacity,.5f)&&q.Lines[1].Opacity==1,"empty queue resets to independent hold and fade");
        q=new DuelSpeechQueue(0);q.Add("A",0);q.Add("B",.5);q.Advance(3.6);q.Add("C",3.6);float paused=q.Lines[1].Opacity;q.Advance(3.8);
        Check(q.Lines[1].Opacity==paused,"switching to sequential preserves paused opacity");
        q.Add("D",3.8);Check(q.Lines.Count==3&&q.Lines[0].Text=="B"&&q.Lines[2].Text=="D","fourth message retains only newest three");
        q.Advance(50);Check(q.Lines.Count==0,"long frame advances all scheduled expiry");
        manager.Begin(DuelMode.AI,false,"127.0.0.1","17951");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        yield return null;yield return null;var player=HelloWorldPlayer.Local;
        player.SendChat("머리 위에도 채팅이 표시됩니다.");
        while(!Has("머리 위에도 채팅이 표시됩니다."))yield return null;yield return null;yield return null;
        var queues=Field<Dictionary<ulong,DuelSpeechQueue>>(chat,"speech");
        Check(queues.ContainsKey(player.OwnerClientId),"server-confirmed message reaches the correct speaker queue");
        var seats=Field<Dictionary<ulong,int>>(chat,"speechSeats");Check(seats[player.OwnerClientId]==player.Seat.Value,"network owner maps to replicated seat");
        var stacks=Field<VisualElement[]>(chat,"speechStacks");var labels=Field<Label[,]>(chat,"speechLabels");
        int seat=player.Seat.Value;
        Check(stacks[seat].style.display.value==DisplayStyle.Flex&&labels[seat,0].text=="머리 위에도 채팅이 표시됩니다.","lobby character shows confirmed bubble");
        chat.Receive(new DuelChatLine{Sequence=100,Sender=player.OwnerClientId,Name=new FixedString64Bytes("나"),Message=new FixedString512Bytes("두 번째 이야기")});
        chat.Receive(new DuelChatLine{Sequence=101,Sender=player.OwnerClientId,Name=new FixedString64Bytes("나"),Message=new FixedString512Bytes("<b>세 번째 이야기</b>")});
        chat.Receive(new DuelChatLine{Sequence=101,Sender=player.OwnerClientId,Name=new FixedString64Bytes("나"),Message=new FixedString512Bytes("중복")});
        yield return null;yield return null;
        Check(queues[player.OwnerClientId].Lines.Count==3&&labels[seat,2].text=="<b>세 번째 이야기</b>","three UI bubbles preserve order and reject duplicate sequence");
        Check(!labels[seat,2].enableRichText&&labels[seat,2].pickingMode==PickingMode.Ignore,"bubble text cannot inject markup or intercept input");
        Check(Mathf.Approximately(labels[seat,2].resolvedStyle.fontSize,17)&&Mathf.Approximately(stacks[seat].resolvedStyle.width,340),"larger font and speech width are applied in rendered UI");
        var hearts=Field<Label[]>(view,"lifeIcons");
        Check(stacks[seat].style.top.value.value<=hearts[seat].style.top.value.value-6,"speech stack is anchored above life/status/name panels");
        var speechLayer=Field<VisualElement>(chat,"speechLayer");
        Check(speechLayer.parent==document.rootVisualElement&&speechLayer.childCount==RouletteRules.MaxPlayers,"speech has one fixed stack per seat");
        chat.Receive(new DuelChatLine{Sequence=102,Sender=9999,Name=new FixedString64Bytes("다른 사람"),Message=new FixedString512Bytes("다른 사람의 글")});
        Check(queues[9999].Lines.Count==1&&queues[player.OwnerClientId].Lines.Count==3,"different players have separate message queues");
        chat.Receive(new DuelChatLine{Sequence=99,Sender=player.OwnerClientId,Name=new FixedString64Bytes("나"),Message=new FixedString512Bytes("늦게 도착한 이전 기록")});
        Check(queues[player.OwnerClientId].Lines[2].Text=="<b>세 번째 이야기</b>","older replay cannot replace newer floating speech");
        yield return Capture("Chat-Speech.png");
        manager.Leave();while(HelloWorldPlayer.Local!=null)yield return null;yield return null;
        Check(queues.Count==0&&speechLayer.style.display.value==DisplayStyle.None,"leaving clears floating chat and hides the layer");
    }
    IEnumerator FreshIme()
    {
        manager.Difficulty=AiDifficulty.Easy;manager.Begin(DuelMode.AI,false,"127.0.0.1","17951");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        yield return null;yield return null;
        TextField previous=null;
        for(int n=0;n<12;n++)
        {
            yield return KeyPress(Key.Enter);
            var field=Field<TextField>(chat,"input");
            var editor=field.Q<VisualElement>(TextField.textInputUssName).Q<TextElement>();
            Check(previous!=field&&TextTarget()==editor,"every activation gets a fresh focused editor");
            Check(field.text.Length==0&&!Field<bool>(chat,"composing"),"second and later activation starts without stale composition");
            Check(Field<Keyboard>(chat,"imeKeyboard")==Keyboard.current,"native IME is re-enabled for this editor");
            foreach(string composing in new[]{"ㅎ","하","한"}){QueueComposition(composing);yield return null;}
            yield return KeyPress(Key.Enter);
            QueueComposition("");yield return null;
            yield return TypeText("한글 연속 입력 "+n);
            yield return new WaitForSecondsRealtime(.2f);
            Check(!chat.Opened&&Has("한글 연속 입력 "+n)&&chat.MessageCount==n+1,"repeated Korean submit reaches room history once");
            Check(Field<Keyboard>(chat,"imeKeyboard")==null,"native IME is ended when chat closes");
            QueueComposition("ㄱ");yield return null;yield return null;
            Check(!Field<bool>(chat,"composing"),"late native composition cannot poison the next activation");
            Check(field.parent!=null&&document.rootVisualElement.Q("room-chat").Query<TextField>().ToList().Count==1,"only one input editor is retained in the panel");
            previous=field;yield return new WaitForSecondsRealtime(.8f);
        }
        yield return KeyPress(Key.Enter);yield return TypeText("남겨 둔 한글");
        var before=Field<TextField>(chat,"input");yield return KeyPress(Key.Escape);yield return KeyPress(Key.Enter);
        var after=Field<TextField>(chat,"input");yield return TypeText(" 초안");
        Check(before!=after&&after.text=="남겨 둔 한글 초안","draft is restored into a fresh editor and typing continues at its end");
        chat.Close();manager.Leave();while(HelloWorldPlayer.Local!=null)yield return null;yield return null;yield return null;
    }
    IEnumerator ImeSubmit()
    {
        manager.Difficulty=AiDifficulty.Easy;manager.Begin(DuelMode.AI,false,"127.0.0.1","17951");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        yield return null;yield return null;
        for(int iteration=0;iteration<3;iteration++)
        {
            yield return KeyPress(Key.Enter);
            var input=Field<TextField>(chat,"input");var editor=input.Q<VisualElement>(TextField.textInputUssName).Q<TextElement>();
            Check(input.text.Length==0,"new chat starts with an empty fresh editor");
            int physical=0;
            EventCallback<KeyDownEvent> observe=e=>{if(e.keyCode==KeyCode.Return&&e.character=='\0')physical++;};
            editor.RegisterCallback(observe);
            TextKey('\0',KeyCode.Return);editor.UnregisterCallback(observe);
            Check(physical==1&&TextTarget()==editor,"physical Enter reaches native text editor without losing focus");
            TextKey('\r');TextKey('\n');yield return null;
            Check(chat.Opened&&TextTarget()==editor,"Enter text characters cannot end editing");
            yield return TypeText("한글 ");
            // Distinct native event batches: compose, press Enter, end composition, final text.
            QueueComposition("확");yield return null;yield return null;
            Check(Field<bool>(chat,"composing"),"IME composition subscription is active");
            yield return KeyPress(Key.Enter);
            Check(chat.Opened&&chat.MessageCount==iteration,"Enter waits for the active composition");
            QueueComposition("");yield return null;yield return null;
            TextKey('확',KeyCode.Return);TextKey('정');TextKey((char)('1'+iteration));
            string expected="한글 확정"+(iteration+1);
            yield return new WaitForSecondsRealtime(.25f);
            Check(!chat.Opened&&chat.MessageCount==iteration+1&&Has(expected),"single Enter sends the complete Korean message exactly once");
            Check(Field<string>(chat,"draft").Length==0,"sent draft is cleared without mutating the old native buffer");
            yield return new WaitForSecondsRealtime(.8f);
        }
        yield return KeyPress(Key.Enter);yield return TypeText("취소할 글");
        QueueComposition("자");yield return null;yield return null;
        yield return KeyPress(Key.Enter);yield return KeyPress(Key.Escape);
        QueueComposition("");yield return null;yield return new WaitForSecondsRealtime(.1f);
        Check(!chat.Opened&&chat.MessageCount==3,"Escape cancels a pending IME send");
        manager.Leave();while(HelloWorldPlayer.Local!=null)yield return null;yield return null;yield return null;
    }
    IEnumerator TextStart()
    {
        manager.Difficulty=AiDifficulty.Easy;manager.Begin(DuelMode.AI,false,"127.0.0.1","17951");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        yield return null;yield return null;
        foreach(char enter in new[]{'\r','\n'})
        {
            yield return KeyPress(Key.Enter);
            var input=Field<TextField>(chat,"input");var editor=input.Q<VisualElement>(TextField.textInputUssName).Q<TextElement>();
            Check(chat.Opened&&TextTarget()==editor,"Enter initially focuses editable leaf");
            // Windows sends the physical Enter and a separate text event with KeyCode.None.
            TextKey(enter);yield return null;
            Check(chat.Opened&&TextTarget()==editor,"trailing Enter character must not leave edit mode");
            var ime=typeof(GUIUtility).GetProperty("imeCompositionMode",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).GetValue(null);
            Check((IMECompositionMode)ime==IMECompositionMode.On,"Enter text must leave IME enabled");
            TextKey('a',KeyCode.A);TextKey('한');yield return null;
            Check(input.value=="a한","first Latin and Korean text events are kept after Enter");
            input.SetValueWithoutNotify("");yield return KeyPress(Key.Escape);
        }
        yield return KeyPress(Key.Enter);
        var currentInput=Field<TextField>(chat,"input");var currentEditor=currentInput.Q<VisualElement>(TextField.textInputUssName).Q<TextElement>();
        QueueComposition("ㅎ");yield return null;yield return null;
        Check(Field<bool>(chat,"composing")&&TextTarget()==currentEditor,"IME start keeps editable focus");
        // A deferred focus attempt must not reposition the cursor during an active composition.
        typeof(DuelChat).GetMethod("FocusInput",F).Invoke(chat,null);
        TextKey('\r');yield return KeyPress(Key.Enter);
        Check(chat.Opened&&chat.MessageCount==0&&TextTarget()==currentEditor,"IME confirmation Enter cannot send or leave editing");
        QueueComposition("");yield return null;yield return null;
        TextKey('한');TextKey('글');yield return null;
        Check(currentInput.value=="한글"&&TextTarget()==currentEditor,"committed Korean text remains editable");
        yield return KeyPress(Key.Enter);yield return new WaitForSecondsRealtime(.2f);
        Check(!chat.Opened&&Has("한글")&&chat.MessageCount==1,"next Enter sends committed Korean once");
        chat.Close();manager.Leave();while(HelloWorldPlayer.Local!=null)yield return null;
        yield return null;yield return null;
    }
    IEnumerator Solo()
    {
        Check(DuelChatLog.Normalize("  한글\n\t대화\u0001  ")=="한글 대화","normalization and control characters");
        string longText="";for(int i=0;i<200;i++)longText+="가😀";
        string limited=DuelChatLog.Normalize(longText);Check(Encoding.UTF8.GetByteCount(limited)<=480&&!char.IsHighSurrogate(limited[limited.Length-1]),"bounded Unicode without splitting surrogate pairs");
        var log=new DuelChatLog();var name=new FixedString64Bytes("이름");var text=new FixedString512Bytes("<b>일반 글자</b>");
        Check(log.TryAppend(1,name,text,0,out var line)&&line.Sequence==1,"server creates identity and order");
        Check(!log.TryAppend(1,name,text,.1,out _)&&log.TryAppend(2,name,text,.1,out _),"per-sender throttle");
        Check(!log.TryAppend(1,name,new FixedString512Bytes("  "),1,out _),"empty message rejected");
        for(int i=1;i<=110;i++)log.TryAppend(1,name,text,i,out _);
        Check(log.History.Count==100&&log.History[99].Sequence==112,"bounded room history");
        log.Clear();Check(log.History.Count==0&&log.TryAppend(1,name,text,0,out line)&&line.Sequence==1,"new room resets history/order/rate limit");
        chat.Open();Check(!chat.Opened,"main menu has no chat");
        manager.Difficulty=AiDifficulty.Easy;manager.Begin(DuelMode.AI,false,"127.0.0.1","17951");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        yield return null;yield return KeyPress(Key.Enter);
        Check(chat.Opened&&DuelChat.BlocksGameplay,"Enter opens lobby chat");
        var input=Field<TextField>(chat,"input");input.value="로비에서 한글 <b>문자</b>";yield return KeyPress(Key.Enter);yield return new WaitForSecondsRealtime(.2f);
        Check(!chat.Opened&&chat.MessageCount==1&&Has("로비에서 한글 <b>문자</b>"),"host submits and receives exactly once");
        Check(!Field<List<Label>>(chat,"labels")[0].enableRichText,"message markup stays literal");
        chat.Open();input=Field<TextField>(chat,"input");input.value="조합 중";
        typeof(DuelChat).GetMethod("Composition",F).Invoke(chat,new object[]{new IMECompositionString("한")});chat.Submit();
        Check(chat.Opened&&chat.MessageCount==1,"IME composition cannot submit");
        typeof(DuelChat).GetMethod("Composition",F).Invoke(chat,new object[]{new IMECompositionString("")});chat.Submit();
        Check(chat.Opened,"IME commit Enter cannot also submit");yield return null;chat.Close();
        for(int i=0;i<105;i++)chat.Receive(new DuelChatLine{Sequence=1000+i,Sender=99,Name=name,Message=new FixedString512Bytes("기록 "+i+" · 채팅 중에는 게임 입력을 차단합니다.")});
        Check(chat.MessageCount==100,"client history bounded");chat.Open();yield return null;yield return null;
        var history=Field<ScrollView>(chat,"history");Check(history.verticalScroller.highValue>0,"long history can scroll");
        history.scrollOffset=Vector2.zero;yield return null;
        chat.Receive(new DuelChatLine{Sequence=1105,Sender=99,Name=name,Message=text});yield return null;yield return null;
        Check(history.scrollOffset.y<5,"incoming chat preserves reading position");
        var layer=Field<VisualElement>(chat,"layer");var results=Field<VisualElement>(view,"resultsLayer");results.BringToFront();yield return null;yield return null;
        Check(layer.parent.hierarchy[layer.parent.hierarchy.childCount-1]==layer,"chat stays above result/game layers");
        manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);HelloWorldPlayer.Local.Rematch();
        while(HelloWorldPlayer.Local.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        manager.ChooseMiniGame(PartyMiniGame.QuickHands);
        while(HelloWorldPlayer.Local.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;
        Check(chat.Opened&&DuelChat.BlocksGameplay,"chat remains active during item turn");
        while(HelloWorldPlayer.Local.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;
        Check(chat.Opened&&UnityEngine.Cursor.lockState==CursorLockMode.None,"game continues while chatting with free cursor");
        var player=HelloWorldPlayer.Local;int before=player.State.Value.ForSeat(0).CorrectInputs;
        yield return KeyPress(Key.Digit1);Check(player.State.Value.ForSeat(0).CorrectInputs==before,"typing digits cannot execute actions");
        yield return Capture("Room-Chat.png");
        int shots=player.State.Value.ForSeat(0).ShotsFired;yield return OutsideClick();
        Check(!chat.Opened&&!DuelChat.BlocksGameplay&&player.State.Value.ForSeat(0).ShotsFired==shots,"world click closes chat without firing");
        var action=player.State.Value.ForSeat(0);yield return KeyPress((Key)((int)Key.Digit1+action.Digit(action.Progress)-1));
        Check(player.State.Value.ForSeat(0).CorrectInputs==before+1,"game input resumes after close");
        yield return KeyPress(Key.Enter);Check(chat.Opened,"Enter reopens chat during minigame");
        yield return KeyPress(Key.Escape);Check(!chat.Opened,"Escape closes chat");
        manager.Leave();while(HelloWorldPlayer.Local!=null)yield return null;yield return null;yield return null;
        Check(chat.MessageCount==0&&layer.style.display.value==DisplayStyle.None,"leaving clears and hides room chat");
    }
    IEnumerator Peers()
    {
        if(role!="host")while(!Seen("host",role=="late"?"remote-message":"initial-message"))yield return null;
        manager.Nickname=role=="host"?"방장":role=="client"?"참가자":"늦은참가";
        manager.Begin(DuelMode.PvP,role!="host","127.0.0.1","17952");
        while(HelloWorldPlayer.Local==null||HelloWorldPlayer.Local.Seat.Value<0)yield return null;
        var player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==39,"matching chat protocol");
        if(role=="host")
        {
            player.SendChat("방장 첫 대화");while(!Has("방장 첫 대화"))yield return null;Mark("initial-message");
            while(!Has("원격 한글 <b>대화</b>"))yield return null;
            Check(Lines.Find(x=>x.Message.ToString()=="원격 한글 <b>대화</b>").Name.ToString()=="참가자","server supplies remote nickname");
            Mark("remote-message");
        }
        else if(role=="client")
        {
            while(!Has("방장 첫 대화"))yield return null;Check(chat.MessageCount==1,"joining receives earlier room history");
            player.SendChat("원격 한글 <b>대화</b>");player.SendChat("연속 전송 차단 대상");
            while(!Has("원격 한글 <b>대화</b>"))yield return null;
        }
        else
        {
            while(!Has("원격 한글 <b>대화</b>"))yield return null;
            Check(Has("방장 첫 대화")&&chat.MessageCount==2,"late player gets both earlier messages once");
            player.SendChat("늦은 참가 확인");
        }
        while(!Has("늦은 참가 확인"))yield return null;
        Check(chat.MessageCount==3&&!Has("연속 전송 차단 대상"),"all peers share ordered messages with rate limit");
        Check(Lines[0].Sequence<Lines[1].Sequence&&Lines[1].Sequence<Lines[2].Sequence,"server sequence order");
        yield return null;yield return null;
        var speechSeats=Field<Dictionary<ulong,int>>(chat,"speechSeats");
        Check(speechSeats.ContainsKey(Lines[0].Sender)&&speechSeats.ContainsKey(Lines[1].Sender)&&speechSeats.ContainsKey(Lines[2].Sender),"host and clients map all three network speakers to seats");
        Check(Field<Dictionary<ulong,DuelSpeechQueue>>(chat,"speech").ContainsKey(Lines[2].Sender),"latest remote message has its own floating speech queue");
        if(role=="host")
        {
            Check(!manager.RequestChat(ulong.MaxValue,new FixedString512Bytes("위조")),"unregistered sender rejected");
            manager.ToggleDeveloperMode();manager.SetRoomManualMiniGame(true);
        }
        while(!player.State.Value.ManualMiniGame)yield return null;player.Rematch();
        while(player.State.Value.Clock.Phase!=ClockPhase.Selecting)yield return null;
        Mark("selecting");
        if(role=="host")
        {
            while(!Seen("client","selecting")||!Seen("late","selecting"))yield return null;
            manager.ChooseMiniGame(PartyMiniGame.QuickHands);
        }
        while(player.State.Value.Clock.Phase!=ClockPhase.ItemPreparation)yield return null;
        chat.Open();Field<TextField>(chat,"input").value="아이템 턴 "+role;chat.Submit();
        while(!Has("아이템 턴 host")||!Has("아이템 턴 client")||!Has("아이템 턴 late"))yield return null;
        Check(true,"all peers chat during item phase");chat.Open();
        while(player.State.Value.Clock.Phase!=ClockPhase.QuickHands)yield return null;
        int before=player.State.Value.ForSeat(player.Seat.Value).CorrectInputs;yield return KeyPress(Key.Digit1);
        Check(player.State.Value.ForSeat(player.Seat.Value).CorrectInputs==before,"chat blocks remote/host numeric input");
        for(int guard=0;guard<60&&player.State.Value.ForSeat(player.Seat.Value).Action!=DuelAction.Aim;guard++)
        {
            var a=player.State.Value.ForSeat(player.Seat.Value);player.InputDigit(a.Digit(a.Progress),a.Version);
            while(player.State.Value.ForSeat(player.Seat.Value).Version==a.Version)yield return null;
        }
        Check(player.State.Value.ForSeat(player.Seat.Value).Action==DuelAction.Aim&&chat.Opened,"chat works in actual aim/shoot phase");
        int shots=player.State.Value.ForSeat(player.Seat.Value).ShotsFired;yield return OutsideClick();
        Check(!chat.Opened&&player.State.Value.ForSeat(player.Seat.Value).ShotsFired==shots,"closing click never fires on each peer");
        yield return KeyPress(Key.Enter);Check(chat.Opened,"Enter opens chat while aiming");
        Field<TextField>(chat,"input").value="사격 턴 "+role;chat.Submit();
        while(!Has("사격 턴 host")||!Has("사격 턴 client")||!Has("사격 턴 late"))yield return null;
        Check(true,"all peers chat during shooting");Mark("verified");
        while(!Seen("host","verified")||!Seen("client","verified")||!Seen("late","verified"))yield return null;
    }
    IEnumerator Capture(string name)
    {
        const int w=1200,h=800;var target=new RenderTexture(w,h,24);target.Create();
        document.panelSettings.scaleMode=PanelScaleMode.ConstantPixelSize;document.panelSettings.scale=1;document.panelSettings.targetTexture=target;document.panelSettings.clearColor=true;document.panelSettings.colorClearValue=Color.clear;
        document.rootVisualElement.style.width=w;document.rootVisualElement.style.height=h;yield return new WaitForSecondsRealtime(.1f);
        var previous=RenderTexture.active;RenderTexture.active=target;var ui=new Texture2D(w,h,TextureFormat.RGBA32,false);ui.ReadPixels(new Rect(0,0,w,h),0,0);ui.Apply();
        var background=new RenderTexture(w,h,24);background.Create();RenderPipeline.SubmitRenderRequest(Camera.main,new UniversalRenderPipeline.SingleCameraRequest{destination=background});
        RenderTexture.active=background;var scene=new Texture2D(w,h,TextureFormat.RGB24,false);scene.ReadPixels(new Rect(0,0,w,h),0,0);scene.Apply();
        var fg=ui.GetPixels();var bg=scene.GetPixels();for(int i=0;i<bg.Length;i++)bg[i]=Color.Lerp(bg[i],fg[i],fg[i].a);scene.SetPixels(bg);scene.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath,"..",name),scene.EncodeToPNG());RenderTexture.active=previous;document.panelSettings.targetTexture=null;
        target.Release();background.Release();Destroy(target);Destroy(background);Destroy(ui);Destroy(scene);
    }
    void Update(){if(deadline>0&&Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL chat "+role+" timeout after "+checks+" checks; "+manager?.Status,1);}
}
