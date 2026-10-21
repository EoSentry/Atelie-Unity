#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace Atelie
{
    // Teclas que o editor usa.
    public enum Hk { Space, Ctrl, Shift, Alt, Z, Y, S, O, E, B, LBracket, RBracket, Delete, Escape, F, X, Enter }

    /// <summary>
    /// Entrada unificada: funciona com o Input System novo e com o Input Manager antigo
    /// (o que estiver ativo em Project Settings &gt; Player &gt; Active Input Handling).
    /// </summary>
    // Entrada unificada: o resto do código chama Inp.X e não precisa saber qual sistema de entrada da Unity está ativo.
    public static class Inp
    {
#if ENABLE_INPUT_SYSTEM
        // Posição do mouse na tela, botões e roda. 'Down'/'Pressed' = apertou neste quadro; 'Held' = está segurando; 'Up' = soltou neste quadro.
        public static Vector2 MousePos { get { return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero; } }
        public static bool LeftDown { get { return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; } }
        public static bool LeftHeld { get { return Mouse.current != null && Mouse.current.leftButton.isPressed; } }
        public static bool LeftUp { get { return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame; } }
        public static bool RightHeld { get { return Mouse.current != null && Mouse.current.rightButton.isPressed; } }
        public static bool MiddleHeld { get { return Mouse.current != null && Mouse.current.middleButton.isPressed; } }
        public static float Scroll
        {
            get
            {
                if (Mouse.current == null) return 0f;
                float y = Mouse.current.scroll.ReadValue().y;
                return Mathf.Clamp(y / 120f, -3f, 3f);
            }
        }

        // Traduz nossas teclas (Hk) para as teclas do sistema da Unity.
        static Key Map(Hk k)
        {
            switch (k)
            {
                case Hk.Space: return Key.Space;
                case Hk.Z: return Key.Z;
                case Hk.Y: return Key.Y;
                case Hk.S: return Key.S;
                case Hk.O: return Key.O;
                case Hk.E: return Key.E;
                case Hk.B: return Key.B;
                case Hk.F: return Key.F;
                case Hk.X: return Key.X;
                case Hk.LBracket: return Key.LeftBracket;
                case Hk.RBracket: return Key.RightBracket;
                case Hk.Delete: return Key.Delete;
                case Hk.Escape: return Key.Escape;
                case Hk.Enter: return Key.Enter;
            }
            return Key.None;
        }

        // Está segurando esta tecla? (Ctrl também vale Command no Mac)
        public static bool Held(Hk k)
        {
            Keyboard kb = Keyboard.current; if (kb == null) return false;
            switch (k)
            {
                case Hk.Ctrl: return kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
                case Hk.Shift: return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                case Hk.Alt: return kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            }
            Key m = Map(k); return m != Key.None && kb[m].isPressed;
        }

        // Apertou esta tecla neste quadro?
        public static bool Pressed(Hk k)
        {
            Keyboard kb = Keyboard.current; if (kb == null) return false;
            Key m = Map(k); return m != Key.None && kb[m].wasPressedThisFrame;
        }
#else
        public static Vector2 MousePos { get { return Input.mousePosition; } }
        public static bool LeftDown { get { return Input.GetMouseButtonDown(0); } }
        public static bool LeftHeld { get { return Input.GetMouseButton(0); } }
        public static bool LeftUp { get { return Input.GetMouseButtonUp(0); } }
        public static bool RightHeld { get { return Input.GetMouseButton(1); } }
        public static bool MiddleHeld { get { return Input.GetMouseButton(2); } }
        public static float Scroll { get { return Input.mouseScrollDelta.y; } }

        static KeyCode Map(Hk k)
        {
            switch (k)
            {
                case Hk.Space: return KeyCode.Space;
                case Hk.Z: return KeyCode.Z;
                case Hk.Y: return KeyCode.Y;
                case Hk.S: return KeyCode.S;
                case Hk.O: return KeyCode.O;
                case Hk.E: return KeyCode.E;
                case Hk.B: return KeyCode.B;
                case Hk.F: return KeyCode.F;
                case Hk.X: return KeyCode.X;
                case Hk.LBracket: return KeyCode.LeftBracket;
                case Hk.RBracket: return KeyCode.RightBracket;
                case Hk.Delete: return KeyCode.Delete;
                case Hk.Escape: return KeyCode.Escape;
                case Hk.Enter: return KeyCode.Return;
            }
            return KeyCode.None;
        }

        public static bool Held(Hk k)
        {
            switch (k)
            {
                case Hk.Ctrl: return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
                case Hk.Shift: return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case Hk.Alt: return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            }
            KeyCode m = Map(k); return m != KeyCode.None && Input.GetKey(m);
        }

        public static bool Pressed(Hk k)
        {
            KeyCode m = Map(k); return m != KeyCode.None && Input.GetKeyDown(m);
        }
#endif
    }
}
