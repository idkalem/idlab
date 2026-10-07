using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace IDLab.App.Interaction
{
    /// <summary>
    /// One pointer abstraction for mouse (desktop) and touch (mobile), over either the legacy Input Manager or
    /// the Input System package. Refreshes lazily once per frame on first access.
    ///   primary pointer  : left mouse / first finger   → pick, drag, turn knobs
    ///   orbit gesture    : right/middle mouse / two-finger drag
    ///   zoom             : scroll wheel / pinch
    /// </summary>
    public static class PointerInput
    {
        private static int _frame = -1;
        private static Vector2 _position, _delta, _orbitDelta;
        private static bool _pressed, _released, _held, _orbitHeld;
        private static float _zoom;
        private static int _touchCount;
        private static float _lastPinch;
        private static Vector2 _lastMouse;

        public static Vector2 Position { get { Refresh(); return _position; } }
        public static Vector2 Delta { get { Refresh(); return _delta; } }
        public static bool Pressed { get { Refresh(); return _pressed; } }
        public static bool Released { get { Refresh(); return _released; } }
        public static bool Held { get { Refresh(); return _held; } }

        /// <summary>Secondary orbit gesture (right/middle drag or two-finger drag).</summary>
        public static bool OrbitHeld { get { Refresh(); return _orbitHeld; } }
        public static Vector2 OrbitDelta { get { Refresh(); return _orbitDelta; } }

        /// <summary>Positive = zoom in.</summary>
        public static float Zoom { get { Refresh(); return _zoom; } }

        public static int TouchCount { get { Refresh(); return _touchCount; } }

        /// <summary>Pointer id for EventSystem.IsPointerOverGameObject (-1 = mouse).</summary>
        public static int PointerId { get; private set; } = -1;

        private static void Refresh()
        {
            if (_frame == Time.frameCount)
            {
                return;
            }

            _frame = Time.frameCount;
            _pressed = _released = _orbitHeld = false;
            _delta = _orbitDelta = Vector2.zero;
            _zoom = 0f;
#if ENABLE_LEGACY_INPUT_MANAGER
            RefreshLegacy();
#elif ENABLE_INPUT_SYSTEM
            RefreshInputSystem();
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private static void RefreshLegacy()
        {
            _touchCount = Input.touchCount;
            if (_touchCount > 0)
            {
                Touch t0 = Input.GetTouch(0);
                PointerId = t0.fingerId;
                if (_touchCount == 1)
                {
                    _position = t0.position;
                    _delta = t0.deltaPosition;
                    _pressed = t0.phase == TouchPhase.Began;
                    _released = t0.phase == TouchPhase.Ended || t0.phase == TouchPhase.Canceled;
                    _held = !_released;
                    _lastPinch = 0f;
                }
                else
                {
                    Touch t1 = Input.GetTouch(1);
                    _released = _held; // a second finger ends any one-finger interaction
                    _held = false;
                    _orbitHeld = true;
                    _orbitDelta = (t0.deltaPosition + t1.deltaPosition) * 0.5f;
                    float pinch = Vector2.Distance(t0.position, t1.position);
                    if (_lastPinch > 0f)
                    {
                        _zoom = (pinch - _lastPinch) * 0.01f;
                    }

                    _lastPinch = pinch;
                }

                return;
            }

            PointerId = -1;
            _lastPinch = 0f;
            Vector2 mouse = Input.mousePosition;
            _delta = mouse - _lastMouse;
            _lastMouse = mouse;
            _position = mouse;
            _pressed = Input.GetMouseButtonDown(0);
            _released = Input.GetMouseButtonUp(0);
            _held = Input.GetMouseButton(0);
            _orbitHeld = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            _orbitDelta = _orbitHeld ? _delta : Vector2.zero;
            _zoom = Input.mouseScrollDelta.y * 0.1f;
        }
#endif

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private static void RefreshInputSystem()
        {
            var touch = Touchscreen.current;
            int active = 0;
            if (touch != null)
            {
                foreach (var t in touch.touches)
                {
                    if (t.press.isPressed || t.press.wasReleasedThisFrame)
                    {
                        active++;
                    }
                }
            }

            _touchCount = active;
            if (active > 0)
            {
                var t0 = touch.touches[0];
                PointerId = t0.touchId.ReadValue();
                if (active == 1)
                {
                    _position = t0.position.ReadValue();
                    _delta = t0.delta.ReadValue();
                    _pressed = t0.press.wasPressedThisFrame;
                    _released = t0.press.wasReleasedThisFrame;
                    _held = t0.press.isPressed;
                    _lastPinch = 0f;
                }
                else
                {
                    var t1 = touch.touches[1];
                    _released = _held;
                    _held = false;
                    _orbitHeld = true;
                    _orbitDelta = (t0.delta.ReadValue() + t1.delta.ReadValue()) * 0.5f;
                    float pinch = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
                    if (_lastPinch > 0f)
                    {
                        _zoom = (pinch - _lastPinch) * 0.01f;
                    }

                    _lastPinch = pinch;
                }

                return;
            }

            PointerId = -1;
            _lastPinch = 0f;
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            _position = mouse.position.ReadValue();
            _delta = mouse.delta.ReadValue();
            _pressed = mouse.leftButton.wasPressedThisFrame;
            _released = mouse.leftButton.wasReleasedThisFrame;
            _held = mouse.leftButton.isPressed;
            _orbitHeld = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            _orbitDelta = _orbitHeld ? _delta : Vector2.zero;
            _zoom = mouse.scroll.ReadValue().y / 1200f;
        }
#endif

        /// <summary>Keyboard axis for tilting a held container (Q / E). Mobile uses the inspector slider.</summary>
        public static float TiltAxis
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
#elif ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k == null ? 0f : (k[Key.E].isPressed ? 1f : 0f) - (k[Key.Q].isPressed ? 1f : 0f);
#else
                return 0f;
#endif
            }
        }

        public static bool DeletePressed
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace);
#elif ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && (k.deleteKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame);
#else
                return false;
#endif
            }
        }
    }
}
