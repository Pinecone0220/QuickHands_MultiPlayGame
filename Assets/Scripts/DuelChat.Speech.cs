using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HelloWorld
{
    public sealed partial class DuelChat
    {
        private VisualElement speechLayer;
        private readonly VisualElement[] speechStacks=new VisualElement[RouletteRules.MaxPlayers];
        private readonly Label[,] speechLabels=new Label[RouletteRules.MaxPlayers,DuelSpeechQueue.Capacity];
        private readonly Dictionary<ulong,DuelSpeechQueue> speech=new Dictionary<ulong,DuelSpeechQueue>();
        private readonly Dictionary<ulong,int> speechSeats=new Dictionary<ulong,int>();
        private readonly List<ulong> expiredSpeech=new List<ulong>();
        private void BuildSpeech()
        {
            speechLayer=new VisualElement{name="player-speech",pickingMode=PickingMode.Ignore};
            speechLayer.AddToClassList("speech-layer");
            var names=root.Q(className:"nameplate-layer");
            root.Insert(names==null?0:root.IndexOf(names)+1,speechLayer);
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
            {
                var stack=new VisualElement{pickingMode=PickingMode.Ignore};
                stack.AddToClassList("speech-stack");speechLayer.Add(stack);speechStacks[seat]=stack;
                for(int i=0;i<DuelSpeechQueue.Capacity;i++)
                {
                    var label=new Label{enableRichText=false,pickingMode=PickingMode.Ignore};
                    label.AddToClassList("speech-line");stack.Add(label);speechLabels[seat,i]=label;
                }
                stack.style.display=DisplayStyle.None;
            }
            speechLayer.style.display=DisplayStyle.None;
        }
        private void PushSpeech(DuelChatLine line)
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(!speech.TryGetValue(line.Sender,out var queue))
            {queue=new DuelSpeechQueue(now);speech.Add(line.Sender,queue);}
            queue.Add(line.Message.ToString(),now);
        }
        private void ClearSpeech()
        {
            speech.Clear();speechSeats.Clear();expiredSpeech.Clear();
            if(speechLayer!=null)speechLayer.style.display=DisplayStyle.None;
            foreach(var stack in speechStacks)if(stack!=null)stack.style.display=DisplayStyle.None;
        }
        private void UpdateSpeech()
        {
            if(speechLayer==null)return;
            if(!Available){speechLayer.style.display=DisplayStyle.None;return;}
            speechLayer.style.display=DisplayStyle.Flex;
            foreach(var stack in speechStacks)stack.style.display=DisplayStyle.None;
            speechSeats.Clear();
            var local=HelloWorldPlayer.Local;var manager=HelloWorldManager.Instance;
            if(manager==null||manager.Table==null||local.NetworkManager.SpawnManager==null)return;
            var view=manager.GetComponent<RouletteView>();
            // Player objects/Seat are replicated to clients. Never use server-only ConnectedClients.
            foreach(var obj in local.NetworkManager.SpawnManager.SpawnedObjectsList)
                if(obj.IsPlayerObject&&obj.TryGetComponent<HelloWorldPlayer>(out var actor)&&actor.IsSpawned&&
                    actor.Seat.Value>=0&&actor.Seat.Value<RouletteRules.MaxPlayers)
                    speechSeats[actor.OwnerClientId]=actor.Seat.Value;
            double now=Time.realtimeSinceStartupAsDouble;expiredSpeech.Clear();
            float width=root.resolvedStyle.width,height=root.resolvedStyle.height;
            foreach(var entry in speech)
            {
                var queue=entry.Value;queue.Advance(now);
                if(queue.Lines.Count==0){expiredSpeech.Add(entry.Key);continue;}
                if(!speechSeats.TryGetValue(entry.Key,out int seat)||!local.State.Value.Connected(seat)||
                    !manager.Table.TryGetNameViewport(seat,out var anchor))continue;
                var stack=speechStacks[seat];stack.style.display=DisplayStyle.Flex;
                float bubbleWidth=Mathf.Min(340,Mathf.Max(1,width-16));stack.style.width=bubbleWidth;
                stack.style.left=Mathf.Clamp(anchor.x*width,bubbleWidth*.5f+8,width-bubbleWidth*.5f-8);
                // Keep speech above health, hearts and any public dice preview.
                float measured=stack.resolvedStyle.height;
                stack.style.top=Mathf.Max(float.IsNaN(measured)?8:measured+8,(1-anchor.y)*height-74-RouletteView.NameplateHealthHeight-(view!=null?view.DiceNameplateHeight(seat):0));
                for(int i=0;i<DuelSpeechQueue.Capacity;i++)
                {
                    var label=speechLabels[seat,i];bool visible=i<queue.Lines.Count;
                    label.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
                    if(visible){label.text=queue.Lines[i].Text;label.style.opacity=queue.Lines[i].Opacity;}
                }
            }
            foreach(ulong sender in expiredSpeech)speech.Remove(sender);
        }
    }
}
