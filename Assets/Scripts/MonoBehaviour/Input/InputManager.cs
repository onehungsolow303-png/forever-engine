using UnityEngine;
using UnityEngine.InputSystem;

namespace ForeverEngine.MonoBehaviour.Input
{
    public class InputManager : UnityEngine.MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        public Vector2Int MoveInput { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool EndTurnPressed { get; private set; }
        public bool ToggleFogPressed { get; private set; }
        public bool ToggleGridPressed { get; private set; }
        public bool TogglePerspective { get; private set; }
        public float ZoomDelta { get; private set; }
        public bool PanActive { get; private set; }
        public Vector2 PanDelta { get; private set; }
        public Vector2 ClickPosition { get; private set; }
        public bool ClickedThisFrame { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            MoveInput = Vector2Int.zero;
            InteractPressed = false;
            EndTurnPressed = false;
            ToggleFogPressed = false;
            ToggleGridPressed = false;
            TogglePerspective = false;
            ZoomDelta = 0f;
            PanDelta = Vector2.zero;
            ClickedThisFrame = false;

            if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.up;
            else if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.down;
            else if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.left;
            else if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
                MoveInput = Vector2Int.right;

            if (Keyboard.current.fKey.wasPressedThisFrame) InteractPressed = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) EndTurnPressed = true;
            if (Keyboard.current.vKey.wasPressedThisFrame) ToggleFogPressed = true;
            if (Keyboard.current.gKey.wasPressedThisFrame) ToggleGridPressed = true;
            if (Keyboard.current.tabKey.wasPressedThisFrame) TogglePerspective = true;

            if (Mouse.current != null)
                ZoomDelta = Mouse.current.scroll.ReadValue().y;

            if (Mouse.current != null)
            {
                PanActive = Mouse.current.middleButton.isPressed;
                if (PanActive)
                    PanDelta = Mouse.current.delta.ReadValue();
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ClickedThisFrame = true;
                ClickPosition = Mouse.current.position.ReadValue();
            }
        }
    }
}
