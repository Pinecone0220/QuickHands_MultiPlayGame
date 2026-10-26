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
        public static bool HasPlayer=>HelloWorldPlayer.Local!=null&&HelloWorldPlayer.Local.IsSpawned&&HelloWorldPlayer.Local.Seat.Value>=0;
        public static bool AltHeld=>Keyboard.current!=null&&(Keyboard.current.leftAltKey.isPressed||Keyboard.current.rightAltKey.isPressed);
        public static bool BlocksActions=>HasPlayer&&(AltHeld||releasedFrame==Time.frameCount);
        private void Awake()
        {
            releasedFrame=-1;root=GetComponent<UIDocument>().rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(e=>Gate(e),TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(e=>Gate(e),TrickleDown.TrickleDown);
        }
        private static void Gate(EventBase e)
        {if(HasPlayer&&!AltHeld){e.StopImmediatePropagation();e.PreventDefault();}}
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
            bool next=HasPlayer&&AltHeld;
            if(held&&!next)
            {
                releasedFrame=Time.frameCount;
                // A release after leaving UI mode is gated; cancel the pending click now.
                var capture=root.panel?.GetCapturingElement(PointerId.mousePointerId) as VisualElement;
                capture?.ReleasePointer(PointerId.mousePointerId);
            }
            held=next;ApplyCursor();
        }
        private void LateUpdate()=>ApplyCursor();
        public static void ApplyCursor()
        {
            bool locked=HasPlayer&&!AltHeld;
            Cursor.lockState=locked?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!locked;
        }
        private void OnDestroy(){releasedFrame=-1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
