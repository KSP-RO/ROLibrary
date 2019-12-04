using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using KSP.UI.TooltipTypes;

namespace ROLib
{
    public abstract class AbstractWindow
    {
        public Rect Position;
        public String Title { get; set; }
        public String Tooltip { get; set; }
        public bool Enabled = false;
        public static GUIStyle Frame = new GUIStyle(HighLogic.Skin.window);
        private readonly Guid mGuid;
        public static Dictionary<Guid, AbstractWindow> Windows = new Dictionary<Guid, AbstractWindow>();
        public static GUIStyle headingStyle, boldBtnStyle, boldLblStyle;

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
                fontSize = 14,
            };
            boldBtnStyle = new GUIStyle(HighLogic.Skin.button)
            {
                fontStyle = FontStyle.Bold,
            };
            boldLblStyle = new GUIStyle(HighLogic.Skin.label)
            {
                fontStyle = FontStyle.Bold,
            };
        }

        public AbstractWindow(Guid id, String title, Rect position)
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

        public Rect RequestPosition() { return Position; }

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

        private void OnHideUI()
        {
            Enabled = false;
        }

        private void OnShowUI()
        {
            Enabled = true;
        }

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
                Window(uid);
            }
            catch (Exception e)
            {
                ROLLog.exc(e);
            }
        }

        public virtual void Window(int uid)
        {
            if (Title != null)
            {
                GUI.DragWindow(new Rect(0, 0, Single.MaxValue, 20));
            }
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

            Position = GUILayout.Window(mGuid.GetHashCode(), Position, WindowPre, Title, Title == null ? Frame : HighLogic.Skin.window);

            if (Title != null)
            {
                if (GUI.Button(new Rect(Position.x + Position.width - 18, Position.y + 2, 16, 16), ""))
                {
                    Hide();
                }
            }
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
            if (this.Enabled)
            {
                this.Hide();
            }
            else
            {
                this.Show();
            }
        }
    }
}
