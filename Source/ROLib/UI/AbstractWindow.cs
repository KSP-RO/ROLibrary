using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROLib
{
    public abstract class AbstractWindow : MonoBehaviour
    {
        private const string InputLockID = "ROLWindowLock";

        public abstract Rect InitialPosition { get; }
        public abstract string Title { get; }

        public Guid Guid { get; private set; }
        public string Tooltip { get; private set; }

        public Rect Position;
        public bool Enabled = false;
        public static Dictionary<Guid, AbstractWindow> Windows = new Dictionary<Guid, AbstractWindow>();

        protected static GUIStyle Frame;
        protected static GUIStyle headingStyle, boldBtnStyle, boldLblStyle, pressedButton;

        // Callback trigger for the change in the position
        public Action onPositionChanged = delegate { };
        protected Rect backupPosition;

        protected void Awake()
        {
            if (Guid == default) Guid = Guid.NewGuid();

            InitGUI();

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
        }

        protected void OnGUI()
        {
            Draw();
        }

        protected void OnDestroy()
        {
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);

            if (Enabled) Hide();
        }

        protected virtual void InitGUI()
        {
            if (Frame == null) InitStaticGUI();

            Position = InitialPosition;
            backupPosition = Position;
        }

        protected virtual void InitStaticGUI()
        {
            Frame = new GUIStyle(HighLogic.Skin.window);
            Frame.padding = new RectOffset(5, 5, 5, 5);
            headingStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 16,
                padding = new RectOffset(0, 0, 0, 10),
            };
            boldBtnStyle = new GUIStyle(HighLogic.Skin.button)
            {
                fontStyle = FontStyle.Bold,
            };
            pressedButton = new GUIStyle(HighLogic.Skin.button)
            {
                fontStyle = FontStyle.Bold,
            };
            pressedButton.normal = pressedButton.active;
            boldLblStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontStyle = FontStyle.Bold,
            };
        }

        public Rect RequestPosition() => Position;

        public virtual void Show()
        {
            if (Enabled)
                return;

            if (Windows.ContainsKey(Guid))
            {
                Windows[Guid].Hide();
            }
            Windows[Guid] = this;
            Enabled = true;
        }

        private void OnHideUI() => Enabled = false;
        private void OnShowUI() => Enabled = true;

        public virtual void Hide()
        {
            Windows.Remove(Guid);
            Enabled = false;
            InputLockManager.RemoveControlLock(InputLockID);
            Destroy(this);
        }

        private void WindowPre(int uid)
        {
            try
            {
                InputLockManager.RemoveControlLock(InputLockID);
                /* Block clicks through window onto ship or other editor UI */
                if (this.backupPosition.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                    InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, InputLockID);

                GUI.skin = HighLogic.Skin;

                using (new GUILayout.VerticalScope())
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(Title ?? "Window", headingStyle);
                        GUILayout.FlexibleSpace();
                        // Close button.
                        if (GUI.Button(GUILayoutUtility.GetRect(18, 18), "\u00d7")) // U+00D7 MULTIPLICATION SIGN
                            Hide();
                    }
                    Window(uid);
                }

            }
            catch (Exception e)
            {
                ROLLog.exc(e);
            }
        }

        public virtual void Window(int uid)
        {
            GUI.DragWindow();
            Tooltip = GUI.tooltip;
        }

        public virtual void Draw()
        {
            if (!Enabled) return;
            if (Event.current.type == EventType.Layout)
            {
                Position.width = 0;
                Position.height = 0;
            }

            Position = GUILayout.Window(Guid.GetHashCode(), Position, WindowPre, new GUIContent(), Frame);

            if (Event.current.type == EventType.Repaint)
            {
                if (Tooltip != "")
                {
                    var pop = GUI.skin.box.alignment;
                    var width = GUI.skin.box.CalcSize(new GUIContent(Tooltip)).x;
                    GUI.skin.box.alignment = TextAnchor.MiddleLeft;
                    GUI.Box(new Rect(Position.x, Position.y + Position.height + 10, width, 28), Tooltip);
                    GUI.skin.box.alignment = pop;
                }

                // Position of the window changed?
                if (!backupPosition.Equals(Position))
                {
                    // trigger the onPositionChanged callbacks
                    onPositionChanged.Invoke();
                    backupPosition = Position;
                }
            }
        }

        /// <summary>
        /// Toggle the window
        /// </summary>
        public void ToggleWindow()
        {
            if (Enabled) Hide();
            else Show();
        }

        public bool RenderToggleButton(string text, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, selected ? pressedButton : HighLogic.Skin.button, options);
        }

        public void RenderGrid(int columns, IEnumerable<Action> items)
        {
            int idx = 0;
            foreach (Action drawItem in items)
            {
                if (idx % columns == 0) GUILayout.BeginHorizontal();
                drawItem();
                if (idx % columns == columns - 1) GUILayout.EndHorizontal();
                idx++;
            }
            if (idx % columns != 0) GUILayout.EndHorizontal(); // Ended on half a row -- close it.
        }

        public T RenderRadioSelectors<T>(T selected, Dictionary<T, string> options, params GUILayoutOption[] toggleOptions)
            where T : Enum
        {
            using (new GUILayout.HorizontalScope())
            {
                foreach (var kvp in options)
                {
                    T option = kvp.Key;
                    string display = kvp.Value;

                    bool isCurrentlySelected = option.Equals(selected);
                    bool toggledOn = GUILayout.Toggle(isCurrentlySelected, display, toggleOptions);

                    // Un-clicked currently selected one, no-op.
                    if (isCurrentlySelected && !toggledOn) return selected;
                    // Clicked a different one, return it.
                    if (!isCurrentlySelected && toggledOn) return option;
                }
            }
            // Nothing clicked; return original selection.
            return selected;
        }
    }
}
