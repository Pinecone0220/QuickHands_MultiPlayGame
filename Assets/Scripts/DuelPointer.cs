using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor=UnityEngine.Cursor;
namespace HelloWorld
{
    [DefaultExecutionOrder(-300)]
    public sealed class DuelPointer:MonoBehaviour
    {
        private static int releasedFrame=-1;
        private bool held;
        private VisualElement root;
        private Texture2D cursorTexture;
        private static readonly Vector2 CursorHotspot=new Vector2(3,3);
        public static bool HasPlayer=>HelloWorldPlayer.Local!=null&&HelloWorldPlayer.Local.IsSpawned&&HelloWorldPlayer.Local.Seat.Value>=0;
        public static bool AltHeld=>Keyboard.current!=null&&(Keyboard.current.leftAltKey.isPressed||Keyboard.current.rightAltKey.isPressed);
        public static bool UiMode=>DuelSettings.WindowOpen||AltHeld||HasPlayer&&HelloWorldPlayer.Local.State.Value.Phase==DuelPhase.Finished;
        public static bool BlocksActions=>HasPlayer&&(UiMode||releasedFrame==Time.frameCount);
        private void Awake()
        {
            releasedFrame=-1;cursorTexture=CreateCursor();Cursor.SetCursor(cursorTexture,CursorHotspot,CursorMode.Auto);root=GetComponent<UIDocument>().rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(e=>Gate(e),TrickleDown.TrickleDown);
        }
        private static void Gate(EventBase e)
        {if(HasPlayer&&!UiMode){e.StopImmediatePropagation();e.PreventDefault();}}
        // Clickable's default left-button filter requires no modifiers, so it rejects Alt-clicks.
        public static void EnableAltClick(Button button)
        {button.clickable.activators.Add(new ManipulatorActivationFilter{button=MouseButton.LeftMouse,modifiers=EventModifiers.Alt});}
        public static void EnableAltClick(Toggle toggle)
        {
            // Toggle does not expose its built-in Clickable. Keep it for normal clicks,
            // and add a separate Alt-only manipulator with the same capture/release semantics.
            var click=new Clickable(()=>toggle.value=!toggle.value);
            click.activators.Clear();
            click.activators.Add(new ManipulatorActivationFilter{button=MouseButton.LeftMouse,modifiers=EventModifiers.Alt});
            toggle.AddManipulator(click);
        }
        private void Update()
        {
            bool next=HasPlayer&&UiMode;
            if(held&&!next)
            {
                releasedFrame=Time.frameCount;
                // A release after leaving UI mode is gated; cancel the pending click now.
                var capture=root.panel?.GetCapturingElement(PointerId.mousePointerId) as VisualElement;
                capture?.ReleasePointer(PointerId.mousePointerId);
            }
            held=next;ApplyCursor();
        }
        private void LateUpdate()
        {
            ApplyCursor();
            if(Cursor.visible&&cursorTexture!=null)Cursor.SetCursor(cursorTexture,CursorHotspot,CursorMode.Auto);
        }
        private static Texture2D CreateCursor()
        {
            const int size=32,samples=4;
            var points=new[]{new Vector2(3,3),new Vector2(28,15),new Vector2(16,16),new Vector2(15,28)};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Color color=Color.clear;
                for(int sy=0;sy<samples;sy++)for(int sx=0;sx<samples;sx++)
                {
                    var p=new Vector2(x+(sx+.5f)/samples,y+(sy+.5f)/samples);bool inside=false;float distance=float.MaxValue;
                    for(int i=0,j=points.Length-1;i<points.Length;j=i++)
                    {
                        var a=points[j];var b=points[i];
                        if((a.y>p.y)!=(b.y>p.y)&&p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                        var edge=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,edge)/edge.sqrMagnitude);distance=Mathf.Min(distance,Vector2.Distance(p,a+edge*t));
                    }
                    if(!inside&&distance>1.15f)continue;
                    Color sample=distance<1.15f?new Color(.075f,.10f,.09f,1):
                        Color.Lerp(new Color(.91f,.73f,.35f,1),new Color(1f,.91f,.68f,1),Mathf.Clamp01((23-p.y)/21));
                    color+=sample/(samples*samples);
                }
                pixels[(size-1-y)*size+x]=color;
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Quick Hands cursor",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
        public static void ApplyCursor()
        {
            bool locked=HasPlayer&&!UiMode;
            Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;
        }
        private void OnDestroy(){releasedFrame=-1;Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);if(cursorTexture!=null)Destroy(cursorTexture);Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
