using System.Collections.Generic;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleSheets;
using UnityEngine.Experimental.UIElements.StyleEnums;
using System.Reflection;
using System.Linq;

namespace UnityEditor.VFX.UI
{
    class StickyNote : GraphElement
    {
        public enum Theme
        {
            Classic,
            Black,
            Orange,
            Green,
            Blue,
            Red,
            Purple,
            Teal
        }

        Theme m_Theme = Theme.Classic;
        public Theme theme
        {
            get
            {
                return m_Theme;
            }
            set
            {
                m_Theme = value;

                UpdateThemeClasses();
            }
        }

        void UpdateThemeClasses()
        {
            foreach(Theme value in System.Enum.GetValues(typeof(Theme)))
            {
                if( m_Theme != value)
                {
                    RemoveFromClassList("theme-"+value.ToString().ToLower());
                }
                else
                {
                    AddToClassList("theme-"+value.ToString().ToLower());
                }
            }
        }

        public static readonly Vector2 defaultSize = new Vector2(100, 100);

        public StickyNote(Vector2 position) : this(UXMLHelper.GetUXMLPath("uxml/StickyNote.uxml"), position)
        {
            AddStyleSheetPath("Selectable");
            AddStyleSheetPath("StickyNote");
        }

        public StickyNote(string uiFile, Vector2 position)
        {
            var tpl = EditorGUIUtility.Load(uiFile) as VisualTreeAsset;

            tpl.CloneTree(this, new Dictionary<string, VisualElement>());

            capabilities = Capabilities.Movable | Capabilities.Resizable | Capabilities.Deletable | Capabilities.Ascendable | Capabilities.Selectable;

            m_Title = this.Q<Label>(name: "title");
            m_Contents = this.Q<Label>(name: "contents");

            if (m_Title != null)
            {
                m_TitleField = m_Title.Q<TextField>(name: "title-field");
                if (m_TitleField != null)
                {
                    m_TitleField.visible = false;
                }
                m_Title.RegisterCallback<MouseDownEvent>(OnTitleMouseDown);
                m_TitleField.RegisterCallback<BlurEvent>(OnTitleBlur);
            }

            if (m_Contents != null)
            {
                m_ContentsField = m_Contents.Q<TextField>(name: "contents-field");
                if (m_ContentsField != null)
                {
                    m_ContentsField.visible = false;
                }
                m_Contents.RegisterCallback<MouseDownEvent>(OnContentsMouseDown);
                m_ContentsField.RegisterCallback<BlurEvent>(OnContentsBlur);
            }

            SetPosition(new Rect(position, defaultSize));

            AddToClassList("sticky-note");
            AddToClassList("selectable");
            UpdateThemeClasses();

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));
        }

        public void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is StickyNote)
            {
                foreach(Theme value in System.Enum.GetValues(typeof(Theme)))
                {
                    evt.menu.AppendAction("Theme/" + value.ToString(), OnChangeTheme, e => ContextualMenu.MenuAction.StatusFlags.Normal,value);
                }
                evt.menu.AppendSeparator();
            }
        }

        void OnChangeTheme(ContextualMenu.MenuAction action)
        {
            theme = (Theme)action.userData;
        }

        void OnTitleBlur(BlurEvent e)
        {
            m_Title.text = m_TitleField.value;
            m_TitleField.visible = false;

            if (!string.IsNullOrEmpty(m_Title.text))
            {
                m_Title.AddToClassList("not-empty");
            }
            else
            {
                m_Title.RemoveFromClassList("not-empty");
            }

            //Notify change
        }

        void OnContentsBlur(BlurEvent e)
        {
            m_Contents.text = m_ContentsField.value;
            m_ContentsField.visible = false;

            //Notify change
        }

        void OnTitleMouseDown(MouseDownEvent e)
        {
            if (e.clickCount == 2)
            {
                m_Title.AddToClassList("not-empty");
                m_TitleField.value = m_Title.text;
                m_TitleField.visible = true;

                m_TitleField.Focus();
                m_TitleField.SelectAll();

                e.StopPropagation();
                e.PreventDefault();
            }
        }

        void OnContentsMouseDown(MouseDownEvent e)
        {
            if (e.clickCount == 2)
            {
                m_ContentsField.value = m_Contents.text;
                m_ContentsField.visible = true;
                m_ContentsField.Focus();
                e.StopPropagation();
                e.PreventDefault();
            }
        }

        Label m_Title;
        TextField m_TitleField;
        Label m_Contents;
        TextField m_ContentsField;
    }
}
