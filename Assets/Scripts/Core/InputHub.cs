using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
// ButtonControl, TouchControl и прочие контролы живут именно здесь.
using UnityEngine.InputSystem.Controls;
#endif

namespace KiviGun
{
    /// <summary>
    /// Единая точка ввода. Работает и со старым Input Manager, и с новым Input System —
    /// проект собирается и запускается в любой конфигурации Unity.
    /// </summary>
    public static class InputHub
    {
        /// <summary>Какой бэкенд ввода включён в проекте (новый Input System или старый).</summary>
        public static bool IsNewInputSystem
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return true;
#else
                return false;
#endif
            }
        }

        // ---------------- Мышь ----------------

        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null ? m.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        public static bool MouseHeld(int button = 0)
        {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                if (m == null) return false;
                switch (button)
                {
                    case 1: return m.rightButton.isPressed;
                    case 2: return m.middleButton.isPressed;
                    default: return m.leftButton.isPressed;
                }
#else
                return Input.GetMouseButton(button);
#endif
        }

        public static bool MousePressed(int button = 0)
        {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                if (m == null) return false;
                switch (button)
                {
                    case 1: return m.rightButton.wasPressedThisFrame;
                    case 2: return m.middleButton.wasPressedThisFrame;
                    default: return m.leftButton.wasPressedThisFrame;
                }
#else
                return Input.GetMouseButtonDown(button);
#endif
        }

        public static bool MouseReleased(int button = 0)
        {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                if (m == null) return false;
                switch (button)
                {
                    case 1: return m.rightButton.wasReleasedThisFrame;
                    case 2: return m.middleButton.wasReleasedThisFrame;
                    default: return m.leftButton.wasReleasedThisFrame;
                }
#else
                return Input.GetMouseButtonUp(button);
#endif
        }

        /// <summary>Смещение мыши за кадр (для обзора).</summary>
        public static Vector2 MouseDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null ? m.delta.ReadValue() : Vector2.zero;
#else
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
            }
        }

        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Mouse m = Mouse.current;
                return m != null ? m.scroll.ReadValue().y : 0f;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        // ---------------- Клавиатура ----------------

        public static bool Key(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = GetKeyControl(key);
            return control != null && control.isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        public static bool KeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = GetKeyControl(key);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        public static bool KeyUp(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = GetKeyControl(key);
            return control != null && control.wasReleasedThisFrame;
#else
            return Input.GetKeyUp(key);
#endif
        }

        public static float AxisX
        {
            get
            {
                float v = 0f;
                if (Key(KeyCode.A) || Key(KeyCode.LeftArrow)) v -= 1f;
                if (Key(KeyCode.D) || Key(KeyCode.RightArrow)) v += 1f;
                return v;
            }
        }

        public static float AxisY
        {
            get
            {
                float v = 0f;
                if (Key(KeyCode.S) || Key(KeyCode.DownArrow)) v -= 1f;
                if (Key(KeyCode.W) || Key(KeyCode.UpArrow)) v += 1f;
                return v;
            }
        }

        public static bool AnyKeyPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
#else
                return Input.anyKeyDown;
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static ButtonControl GetKeyControl(KeyCode key)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return null;
            switch (key)
            {
                case KeyCode.W: return kb.wKey;
                case KeyCode.A: return kb.aKey;
                case KeyCode.S: return kb.sKey;
                case KeyCode.D: return kb.dKey;
                case KeyCode.Q: return kb.qKey;
                case KeyCode.E: return kb.eKey;
                case KeyCode.R: return kb.rKey;
                case KeyCode.F: return kb.fKey;
                case KeyCode.C: return kb.cKey;
                case KeyCode.V: return kb.vKey;
                case KeyCode.T: return kb.tKey;
                case KeyCode.Z: return kb.zKey;
                case KeyCode.Space: return kb.spaceKey;
                case KeyCode.LeftShift: return kb.leftShiftKey;
                case KeyCode.RightShift: return kb.rightShiftKey;
                case KeyCode.LeftControl: return kb.leftCtrlKey;
                case KeyCode.RightControl: return kb.rightCtrlKey;
                case KeyCode.Escape: return kb.escapeKey;
                case KeyCode.Return: return kb.enterKey;
                case KeyCode.Tab: return kb.tabKey;
                case KeyCode.LeftArrow: return kb.leftArrowKey;
                case KeyCode.RightArrow: return kb.rightArrowKey;
                case KeyCode.UpArrow: return kb.upArrowKey;
                case KeyCode.DownArrow: return kb.downArrowKey;
                case KeyCode.Alpha1: return kb.digit1Key;
                case KeyCode.Alpha2: return kb.digit2Key;
                case KeyCode.Alpha3: return kb.digit3Key;
                case KeyCode.Alpha4: return kb.digit4Key;
                case KeyCode.Alpha5: return kb.digit5Key;
                default: return null;
            }
        }
#endif

        // ---------------- Касания (мобильные) ----------------
        //
        // В новом Input System касания лежат в «слотах» (touches[0..9]), и порядковый
        // номер активного касания ≠ номер слота. Если их не сопоставить, джойстик
        // и прицел начнут следить за разными пальцами. Поэтому каждому порядковому
        // номеру находим свой слот. Слоты с фазой None считаем пустыми.

#if ENABLE_INPUT_SYSTEM
        /// <summary>Слот касания по порядковому номеру (-1, если такого касания нет).</summary>
        private static int TouchSlot(int ordinal)
        {
            Touchscreen ts = Touchscreen.current;
            if (ts == null || ordinal < 0) return -1;

            int found = 0;
            for (int i = 0; i < ts.touches.Count; i++)
            {
                if (ts.touches[i].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.None) continue;
                if (found == ordinal) return i;
                found++;
            }
            return -1;
        }
#endif

        // ---------------- Касания по СЛОТУ (стабильный идентификатор пальца) ----------------
        //
        // Порядковый номер касания НЕ стабилен: стоит отпустить один палец,
        // и номера остальных сдвигаются (первый становится вторым и т.д.).
        // Если хранить «номер пальца» в переменной, джойстик и обзор
        // начнут следить за разными пальцами или за уже отпущенным.
        // Поэтому ниже — работа по СЛОТУ (номер касания в устройстве),
        // он не меняется, пока палец на экране.

        /// <summary>Слот устройства по порядковому номеру (-1, если касания нет).</summary>
        public static int TouchSlotOf(int ordinal)
        {
#if ENABLE_INPUT_SYSTEM
            return TouchSlot(ordinal);
#else
            if (ordinal < 0 || ordinal >= Input.touchCount) return -1;
            return ordinal;
#endif
        }

        /// <summary>Касание в этом слоте ещё на экране?</summary>
        public static bool TouchActive(int slot)
        {
            if (slot < 0) return false;
#if ENABLE_INPUT_SYSTEM
            Touchscreen ts = Touchscreen.current;
            if (ts == null || slot >= ts.touches.Count) return false;
            return ts.touches[slot].phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None;
#else
            return slot < Input.touchCount;
#endif
        }

        /// <summary>Позиция касания по слоту.</summary>
        public static Vector2 TouchPositionBySlot(int slot)
        {
            if (slot < 0) return Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            Touchscreen ts = Touchscreen.current;
            if (ts == null || slot >= ts.touches.Count) return Vector2.zero;
            return ts.touches[slot].position.ReadValue();
#else
            return slot < Input.touchCount ? Input.GetTouch(slot).position : Vector2.zero;
#endif
        }

        /// <summary>Касание только что началось (по слоту).</summary>
        public static bool TouchBeganBySlot(int slot)
        {
            if (slot < 0) return false;
#if ENABLE_INPUT_SYSTEM
            Touchscreen ts = Touchscreen.current;
            if (ts == null || slot >= ts.touches.Count) return false;
            return ts.touches[slot].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began;
#else
            return slot < Input.touchCount && Input.GetTouch(slot).phase == TouchPhase.Began;
#endif
        }

        /// <summary>Касание только что закончилось (по слоту).</summary>
        public static bool TouchEndedBySlot(int slot)
        {
            if (slot < 0) return false;
#if ENABLE_INPUT_SYSTEM
            Touchscreen ts = Touchscreen.current;
            if (ts == null || slot >= ts.touches.Count) return false;
            var phase = ts.touches[slot].phase.ReadValue();
            return phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
#else
            return slot < Input.touchCount && (Input.GetTouch(slot).phase == TouchPhase.Ended ||
                                               Input.GetTouch(slot).phase == TouchPhase.Canceled);
#endif
        }

        public static int TouchCount
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Touchscreen ts = Touchscreen.current;
                if (ts == null) return 0;
                int n = 0;
                for (int i = 0; i < ts.touches.Count; i++)
                    if (ts.touches[i].phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None) n++;
                return n;
#else
                return Input.touchCount;
#endif
            }
        }

        public static Vector2 TouchPosition(int index)
        {
#if ENABLE_INPUT_SYSTEM
                int slot = TouchSlot(index);
                if (slot < 0) return Vector2.zero;
                return Touchscreen.current.touches[slot].position.ReadValue();
#else
                return index >= 0 && index < Input.touchCount ? Input.GetTouch(index).position : Vector2.zero;
#endif
        }

        public static bool TouchBegan(int index)
        {
#if ENABLE_INPUT_SYSTEM
                int slot = TouchSlot(index);
                if (slot < 0) return false;
                return Touchscreen.current.touches[slot].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began;
#else
                return index >= 0 && index < Input.touchCount && Input.GetTouch(index).phase == TouchPhase.Began;
#endif
        }

        public static bool TouchEnded(int index)
        {
#if ENABLE_INPUT_SYSTEM
                int slot = TouchSlot(index);
                if (slot < 0) return false;
                var phase = Touchscreen.current.touches[slot].phase.ReadValue();
                return phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
#else
                return index >= 0 && index < Input.touchCount && (Input.GetTouch(index).phase == TouchPhase.Ended || Input.GetTouch(index).phase == TouchPhase.Canceled);
#endif
        }

        // ---------------- Универсальный указатель (мышь ИЛИ палец) ----------------
        //
        // Интерфейс должен работать и на компьютере, и на телефоне, поэтому
        // «куда нажали» — это мышь, если её двигают, иначе палец.

        /// <summary>Позиция указателя: палец важнее мыши.</summary>
        public static Vector2 PointerPosition
        {
            get
            {
                if (TouchCount > 0) return TouchPosition(0);
                return MousePosition;
            }
        }

        /// <summary>Указатель зажат (кнопка мыши или палец на экране).</summary>
        public static bool PointerHeld
        {
            get
            {
                if (TouchCount > 0) return true;
                return MouseHeld(0);
            }
        }

        /// <summary>Указатель только что отпустили.</summary>
        public static bool PointerReleased
        {
            get
            {
                if (TouchCount > 0) return TouchEnded(0);
                return MouseReleased(0);
            }
        }
    }
}
