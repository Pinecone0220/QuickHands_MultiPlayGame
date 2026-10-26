using UnityEngine;
using UnityEngine.UIElements;

namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private VisualElement lifeShade,lifeCounter,lifeNumberStrip;
        private int shownLifeFrom=-1,shownLifeTo=-1;
        internal void SetLifeFeedback(float strength,float opacity,int from,int remaining,float scroll)
        {
            if(root==null)return;
            if(lifeShade==null)
            {
                lifeShade=new VisualElement{name="life-collapse-shade",pickingMode=PickingMode.Ignore};
                lifeShade.style.position=Position.Absolute;lifeShade.style.left=lifeShade.style.right=lifeShade.style.top=lifeShade.style.bottom=0;
                lifeShade.style.backgroundColor=Color.black;root.Insert(0,lifeShade);
                lifeCounter=Box(root,"life-counter");lifeCounter.name="life-counter";lifeCounter.pickingMode=PickingMode.Ignore;
                Text(lifeCounter,"목숨 ×","life-counter-title").pickingMode=PickingMode.Ignore;
                var window=Box(lifeCounter,"life-number-window");window.pickingMode=PickingMode.Ignore;
                lifeNumberStrip=Box(window,"life-number-strip");lifeNumberStrip.pickingMode=PickingMode.Ignore;
            }
            lifeShade.style.opacity=strength*.18f;Show(lifeShade,strength>0);
            Show(lifeCounter,opacity>0);lifeCounter.style.opacity=opacity;
            if(from!=shownLifeFrom||remaining!=shownLifeTo)
            {
                shownLifeFrom=from;shownLifeTo=remaining;lifeNumberStrip.Clear();
                for(int n=remaining;n<=from;n++)Text(lifeNumberStrip,n.ToString(),"life-counter-number").pickingMode=PickingMode.Ignore;
            }
            // New numbers enter from above; the words and multiplication sign stay still.
            lifeNumberStrip.style.translate=new Translate(0,-72*Mathf.Max(0,from-remaining)*(1-scroll),0);
            if(opacity>0&&victoryText.ClassListContains("death-banner"))Show(victoryLayer,false);
        }
    }
}
