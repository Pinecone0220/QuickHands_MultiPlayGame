using UnityEngine;
using UnityEngine.UIElements;
namespace HelloWorld
{
    public sealed partial class RouletteView
    {
        private Button quitGame,quitYes,quitNo;
        private VisualElement quitConfirmLayer;
        private bool quitConfirmOpen;
        private void BuildQuitConfirmation()
        {
            quitGame=Button(root,"게임 종료",()=>SetQuitConfirmation(true),"quit-game");
            quitGame.AddToClassList("secondary");
            quitConfirmLayer=Box(root,"weapon-info-layer");
            var panel=Box(quitConfirmLayer,"quit-confirm-panel");
            Text(panel,"게임을 종료하시겠습니까?","quit-confirm-title");
            var row=Box(panel,"button-row");
            quitYes=Button(row,"예",ConfirmQuit,"primary");
            quitNo=Button(row,"아니오",()=>SetQuitConfirmation(false),"secondary");
            Show(quitConfirmLayer,false);
        }
        private void SetQuitConfirmation(bool visible)
        {
            if(visible&&manager.Busy)return;
            quitConfirmOpen=visible;Show(quitConfirmLayer,visible);
            if(visible){quitConfirmLayer.BringToFront();quitNo.Focus();}
            else if(!manager.Busy)quitGame.Focus();
        }
        private void UpdateMenuQuit()
        {
            bool mainMenu=!manager.Busy;
            fullscreen.EnableInClassList("menu-fullscreen",mainMenu);
            Show(quitGame,mainMenu);
            if(!mainMenu&&quitConfirmOpen)SetQuitConfirmation(false);
            if(quitConfirmOpen)quitConfirmLayer.BringToFront();
        }
        private void ConfirmQuit()
        {
            if(!quitConfirmOpen||manager.Busy)return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
    }
}
