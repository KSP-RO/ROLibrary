using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROLib
{
    public abstract class AbstractWindow
    {
        public Rect Position;
        public string Title { get; set; }
        public string Tooltip { get; set; }
        public bool Enabled = false;
        public static GUIStyle Frame = new GUIStyle(HighLogic.Skin.window);
        private readonly Guid mGuid;
        public static Dictionary<Guid, AbstractWindow> Windows = new Dictionary<Guid, AbstractWindow>();
        public static GUIStyle headingStyle, boldBtnStyle, boldLblStyle, pressedButton;

        // Initial width and height of the window
        public float mInitialWidth;
        public float mInitialHeight;

        // Callback trigger for the change in the position
        public Action onPositionChanged = delegate { };
        public Rect backupPosition;

        static AbstractWindow()
        {
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

        public AbstractWindow(Guid id, string title, Rect position)
        {
            mGuid = id;
            Title = title;
            Position = position;
            backupPosition = position;
            mInitialHeight = position.height + 15;
            mInitialWidth = position.width + 15;

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
        }

        public Rect RequestPosition() => Position;

        public virtual void Show()
        {
            if (Enabled)
                return;

            if (Windows.ContainsKey(mGuid))
            {
                Windows[mGuid].Hide();
            }
            Windows[mGuid] = this;
            Enabled = true;
        }

        private void OnHideUI() => Enabled = false;
        private void OnShowUI() => Enabled = true;

        public virtual void Hide()
        {
            Windows.Remove(mGuid);
            Enabled = false;
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            InputLockManager.RemoveControlLock("ROLWindowLock");
        }

        private void WindowPre(int uid)
        {
            try
            {
                InputLockManager.RemoveControlLock("ROLWindowLock");
                /* Block clicks through window onto ship or other editor UI */
                if (this.backupPosition.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                    InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, "ROLWindowLock");

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

            Position = GUILayout.Window(mGuid.GetHashCode(), Position, WindowPre, new GUIContent(), Frame);

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
        public void toggleWindow()
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
