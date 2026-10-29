/*
   Copyright 2016-2022 LIPtoH <liptoh.codebase@gmail.com>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TS_SE_Tool.Utilities
{
    public enum UIThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    /// <summary>
    /// Central place for the look of the whole program interface.
    /// Call UITheme.Apply(this) at the end of a form constructor and the form (including
    /// every control that is added later) is painted in the selected theme.
    /// </summary>
    public static class UITheme
    {
        #region Colours

        public static Color FormBackground { get { return IsDark ? FromHex(0x1E1F22) : FromHex(0xF5F6F8); } }

        public static Color Surface { get { return IsDark ? FromHex(0x2B2D31) : FromHex(0xFFFFFF); } }

        public static Color InputBackground { get { return IsDark ? FromHex(0x25272A) : FromHex(0xFFFFFF); } }

        public static Color TextColor { get { return IsDark ? FromHex(0xE8E8EA) : FromHex(0x1F2328); } }

        public static Color SubtleTextColor { get { return IsDark ? FromHex(0x9AA0A8) : FromHex(0x5A6069); } }

        public static Color DisabledTextColor { get { return IsDark ? FromHex(0x6C7076) : FromHex(0x9AA0A8); } }

        public static Color BorderColor { get { return IsDark ? FromHex(0x3E4147) : FromHex(0xD5D8DD); } }

        public static Color AccentColor { get { return FromHex(0x2F81F7); } }

        public static Color AccentTextColor { get { return Color.White; } }

        public static Color HighlightColor { get { return IsDark ? FromHex(0x35393E) : FromHex(0xE8EEF7); } }

        public static Color MenuBackground { get { return IsDark ? FromHex(0x18191C) : FromHex(0xFFFFFF); } }

        /// <summary>Colour of neutral (informational) text like the status bar message.</summary>
        public static Color StatusTextColor { get { return TextColor; } }

        /// <summary>Colour of error text, readable on both themes.</summary>
        public static Color StatusErrorColor { get { return IsDark ? FromHex(0xFF6B6B) : Color.Red; } }

        /// <summary>Colour of success text, readable on both themes.</summary>
        public static Color StatusSuccessColor { get { return IsDark ? FromHex(0x63C97A) : Color.DarkGreen; } }

        private static Color FromHex(int _rgb)
        {
            return Color.FromArgb((_rgb >> 16) & 0xFF, (_rgb >> 8) & 0xFF, _rgb & 0xFF);
        }

        #endregion

        #region Mode

        private static UIThemeMode currentMode = UIThemeMode.System;

        public static UIThemeMode Mode
        {
            get { return currentMode; }
            set { currentMode = value; }
        }

        public static bool IsDark
        {
            get { return currentMode == UIThemeMode.Dark || (currentMode == UIThemeMode.System && SystemUsesDarkApps()); }
        }

        public static UIThemeMode ParseMode(string _mode)
        {
            if (string.IsNullOrEmpty(_mode))
                return UIThemeMode.System;

            switch (_mode.Trim().ToLowerInvariant())
            {
                case "light":
                    return UIThemeMode.Light;

                case "dark":
                    return UIThemeMode.Dark;

                default:
                    return UIThemeMode.System;
            }
        }

        public static string ModeToString(UIThemeMode _mode)
        {
            switch (_mode)
            {
                case UIThemeMode.Light:
                    return "Light";

                case UIThemeMode.Dark:
                    return "Dark";

                default:
                    return "System";
            }
        }

        private static bool SystemUsesDarkApps()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");

                        if (value is int)
                            return (int)value == 0;
                    }
                }
            }
            catch
            { }

            return false;
        }

        #endregion

        #region Bookkeeping

        private static readonly object Marker = new object();

        //Controls that got an event handler from us, so we attach it only once.
        private static readonly ConditionalWeakTable<Control, object> hookedControls = new ConditionalWeakTable<Control, object>();

        //Controls whose colours we changed, so we know a colour was set by the program and must be left alone.
        private static readonly ConditionalWeakTable<Control, StyledInfo> styledControls = new ConditionalWeakTable<Control, StyledInfo>();

        private sealed class StyledInfo
        {
            public Color BackColor = Color.Empty;
            public Color ForeColor = Color.Empty;
        }

        private static bool Mark(ConditionalWeakTable<Control, object> _table, Control _control)
        {
            object marker;

            if (_table.TryGetValue(_control, out marker))
                return false;

            _table.Add(_control, Marker);
            return true;
        }

        /// <summary>
        /// True when the colour of a control may be replaced: it is still the default one
        /// or one this theme has set itself. Colours set by the program (saved colours,
        /// warning text and so on) are never touched.
        /// </summary>
        private static bool MayStyle(Control _control, bool _background)
        {
            StyledInfo info;
            styledControls.TryGetValue(_control, out info);

            Color current = _background ? _control.BackColor : _control.ForeColor;
            Color ours = info == null ? Color.Empty : (_background ? info.BackColor : info.ForeColor);
            Color defaultColor = _background ? SystemColors.Control : SystemColors.ControlText;

            if (!ours.IsEmpty && current.ToArgb() == ours.ToArgb())
                return true;

            return current.ToArgb() == defaultColor.ToArgb();
        }

        private static void RecordStyle(Control _control, bool _background, Color _color)
        {
            StyledInfo info;

            if (!styledControls.TryGetValue(_control, out info))
            {
                info = new StyledInfo();
                styledControls.Add(_control, info);
            }

            if (_background)
                info.BackColor = _color;
            else
                info.ForeColor = _color;
        }

        private static void SetBackground(Control _control, Color _color)
        {
            if (!MayStyle(_control, true))
                return;

            _control.BackColor = _color;
            RecordStyle(_control, true, _color);
        }

        private static void SetForeground(Control _control, Color _color)
        {
            if (!MayStyle(_control, false))
                return;

            _control.ForeColor = _color;
            RecordStyle(_control, false, _color);
        }

        #endregion

        #region Entry points

        /// <summary>Paints a form and everything in it.</summary>
        public static void Apply(Form _form)
        {
            if (_form == null)
                return;

            try
            {
                ApplyRenderers();
                ApplyToControl(_form);
                ApplyTitleBar(_form);
            }
            catch
            { }
        }

        /// <summary>Repaints every form that is open, used after the theme was changed.</summary>
        public static void ApplyToOpenForms()
        {
            try
            {
                ApplyRenderers();

                foreach (Form form in Application.OpenForms)
                    Apply(form);
            }
            catch
            { }
        }

        #endregion

        #region Walking controls

        private static void ApplyToControl(Control _control)
        {
            if (_control == null)
                return;

            try
            {
                if (Mark(hookedControls, _control))
                {
                    _control.ControlAdded += ControlAddedHandler;

                    Form form = _control as Form;

                    if (form != null)
                        form.Shown += FormShownHandler;
                }

                StyleControl(_control);

                foreach (Control child in _control.Controls)
                    ApplyToControl(child);
            }
            catch
            { }
        }

        private static void ControlAddedHandler(object sender, ControlEventArgs e)
        {
            ApplyToControl(e.Control);
        }

        private static void FormShownHandler(object sender, EventArgs e)
        {
            ApplyToControl(sender as Control);
        }

        private static void StyleControl(Control _control)
        {
            if (_control == null)
                return;

            //Images are painted by the program itself.
            if (_control is PictureBox)
                return;

            if (_control is Form)
            {
                SetBackground(_control, FormBackground);
                SetForeground(_control, TextColor);
                return;
            }

            if (_control is TabControl)
            {
                StyleTabControl((TabControl)_control);
                return;
            }

            if (_control is TabPage)
            {
                SetBackground(_control, FormBackground);
                SetForeground(_control, TextColor);
                return;
            }

            if (_control is ToolStrip)             // MenuStrip, StatusStrip, ContextMenuStrip and tool bars
            {
                StyleToolStrip((ToolStrip)_control);
                return;
            }

            if (_control is ButtonBase)
            {
                StyleButtonBase((ButtonBase)_control);
                return;
            }

            if (_control is TextBoxBase)
            {
                TextBoxBase textBox = (TextBoxBase)_control;

                SetBackground(textBox, InputBackground);
                SetForeground(textBox, TextColor);

                TextBox singleLineBox = textBox as TextBox;

                if (singleLineBox != null && singleLineBox.BorderStyle == BorderStyle.Fixed3D)
                    singleLineBox.BorderStyle = BorderStyle.FixedSingle;

                return;
            }

            if (_control is ListControl)           // ComboBox, ListBox, CheckedListBox
            {
                ListControl list = (ListControl)_control;

                SetBackground(list, InputBackground);
                SetForeground(list, TextColor);

                ListBox listBox = list as ListBox;

                if (listBox != null && listBox.BorderStyle == BorderStyle.Fixed3D)
                    listBox.BorderStyle = BorderStyle.FixedSingle;

                ComboBox comboBox = list as ComboBox;

                if (comboBox != null)
                    comboBox.FlatStyle = FlatStyle.Flat;

                return;
            }

            if (_control is NumericUpDown)
            {
                SetBackground(_control, InputBackground);
                SetForeground(_control, TextColor);
                return;
            }

            if (_control is DataGridView)
            {
                StyleDataGridView((DataGridView)_control);
                return;
            }

            if (_control is TreeView)
            {
                TreeView tree = (TreeView)_control;

                SetBackground(tree, InputBackground);
                SetForeground(tree, TextColor);

                tree.LineColor = BorderColor;

                if (tree.BorderStyle == BorderStyle.Fixed3D)
                    tree.BorderStyle = BorderStyle.FixedSingle;

                return;
            }

            if (_control is Label)                 // Label and LinkLabel
            {
                SetForeground(_control, TextColor);

                LinkLabel link = _control as LinkLabel;

                if (link != null)
                {
                    link.LinkColor = IsDark ? FromHex(0x8AB4F8) : AccentColor;
                    link.ActiveLinkColor = AccentColor;
                    link.VisitedLinkColor = IsDark ? FromHex(0xB9A6F2) : FromHex(0x6E40C9);
                }

                return;
            }

            if (_control is GroupBox || _control is Panel || _control is SplitContainer || _control is SplitterPanel
                || _control is PropertyGrid || _control is UserControl)
            {
                SetBackground(_control, FormBackground);
                SetForeground(_control, TextColor);
                return;
            }
        }

        private static void StyleButtonBase(ButtonBase _button)
        {
            bool asButton = false;

            CheckBox checkBox = _button as CheckBox;
            RadioButton radioButton = _button as RadioButton;

            if (checkBox != null)
                asButton = checkBox.Appearance == Appearance.Button;
            else if (radioButton != null)
                asButton = radioButton.Appearance == Appearance.Button;

            //Buttons that carry a picture keep their own colours, otherwise the picture could become invisible.
            if (_button.BackgroundImage != null || (asButton && _button.Image != null))
                return;

            _button.FlatStyle = FlatStyle.Flat;
            _button.FlatAppearance.BorderSize = 1;
            _button.FlatAppearance.BorderColor = BorderColor;
            _button.FlatAppearance.MouseOverBackColor = HighlightColor;
            _button.FlatAppearance.MouseDownBackColor = BorderColor;

            if (asButton)
            {
                SetBackground(_button, Surface);
                SetForeground(_button, TextColor);
            }
            else if (_button is Button)
            {
                SetBackground(_button, Surface);
                SetForeground(_button, TextColor);
            }
            else
            {
                //Plain check box or radio button takes the colour of its parent.
                SetBackground(_button, Color.Transparent);
                SetForeground(_button, TextColor);
            }
        }

        private static void StyleDataGridView(DataGridView _grid)
        {
            try
            {
                _grid.BackgroundColor = FormBackground;
                _grid.GridColor = BorderColor;
                _grid.EnableHeadersVisualStyles = false;

                _grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
                _grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
                _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AccentColor;
                _grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AccentTextColor;

                _grid.DefaultCellStyle.BackColor = InputBackground;
                _grid.DefaultCellStyle.ForeColor = TextColor;
                _grid.DefaultCellStyle.SelectionBackColor = AccentColor;
                _grid.DefaultCellStyle.SelectionForeColor = AccentTextColor;

                _grid.RowHeadersDefaultCellStyle.BackColor = Surface;
                _grid.RowHeadersDefaultCellStyle.ForeColor = TextColor;
                _grid.RowHeadersDefaultCellStyle.SelectionBackColor = AccentColor;
                _grid.RowHeadersDefaultCellStyle.SelectionForeColor = AccentTextColor;
            }
            catch
            { }
        }

        #endregion

        #region Tab pages

        private static void StyleTabControl(TabControl _tabControl)
        {
            try
            {
                _tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;

                _tabControl.DrawItem -= TabControlDrawItem;
                _tabControl.DrawItem += TabControlDrawItem;

                _tabControl.SizeChanged -= TabControlSizeChanged;
                _tabControl.SizeChanged += TabControlSizeChanged;

                SetBackground(_tabControl, FormBackground);
                SetForeground(_tabControl, TextColor);

                UpdateTabSizes(_tabControl);

                foreach (TabPage page in _tabControl.TabPages)
                {
                    SetBackground(page, FormBackground);
                    SetForeground(page, TextColor);
                }
            }
            catch
            { }
        }

        private static void TabControlSizeChanged(object sender, EventArgs e)
        {
            UpdateTabSizes(sender as TabControl);
        }

        /// <summary>Keeps the tab headers the same width so they fill the strip evenly.</summary>
        private static void UpdateTabSizes(TabControl _tabControl)
        {
            try
            {
                if (_tabControl == null || _tabControl.TabPages.Count == 0)
                    return;

                int available = _tabControl.ClientSize.Width;

                if (available <= 0)
                    return;

                int width = Math.Max(70, (available - 4) / _tabControl.TabPages.Count);
                int height = Math.Max(24, _tabControl.Font.Height + 11);

                if (_tabControl.ItemSize.Width == width && _tabControl.ItemSize.Height == height)
                    return;

                _tabControl.SizeMode = TabSizeMode.Fixed;
                _tabControl.ItemSize = new Size(width, height);
            }
            catch
            { }
        }

        private static void TabControlDrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tabControl = sender as TabControl;

            if (tabControl == null || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
                return;

            try
            {
                Rectangle tabRect = tabControl.GetTabRect(e.Index);
                bool selected = tabControl.SelectedIndex == e.Index;

                //Background of the whole strip, so no system colour is left next to the last tab.
                Rectangle stripRect;

                if (tabControl.Alignment == TabAlignment.Bottom)
                    stripRect = new Rectangle(0, tabRect.Top, tabControl.Width, tabControl.Height - tabRect.Top);
                else if (tabControl.Alignment == TabAlignment.Top)
                    stripRect = new Rectangle(0, 0, tabControl.Width, tabRect.Bottom);
                else
                    stripRect = tabRect;

                using (SolidBrush stripBrush = new SolidBrush(FormBackground))
                    e.Graphics.FillRectangle(stripBrush, stripRect);

                using (SolidBrush tabBrush = new SolidBrush(selected ? Surface : FormBackground))
                    e.Graphics.FillRectangle(tabBrush, tabRect);

                if (selected)
                {
                    Rectangle indicator = tabControl.Alignment == TabAlignment.Bottom
                        ? new Rectangle(tabRect.Left + 6, tabRect.Top, Math.Max(0, tabRect.Width - 12), 2)
                        : new Rectangle(tabRect.Left + 6, tabRect.Bottom - 2, Math.Max(0, tabRect.Width - 12), 2);

                    using (SolidBrush accentBrush = new SolidBrush(AccentColor))
                        e.Graphics.FillRectangle(accentBrush, indicator);
                }

                TabPage page = tabControl.TabPages[e.Index];

                int textLeft = tabRect.Left + 8;
                int textRight = tabRect.Right - 8;

                if (tabControl.ImageList != null && page.ImageIndex >= 0 && page.ImageIndex < tabControl.ImageList.Images.Count)
                {
                    Image tabImage = tabControl.ImageList.Images[page.ImageIndex];

                    if (tabImage != null)
                    {
                        e.Graphics.DrawImage(tabImage, new Rectangle(textLeft, tabRect.Top + (tabRect.Height - 16) / 2, 16, 16));
                        textLeft += 20;
                    }
                }

                Rectangle textRect = new Rectangle(textLeft, tabRect.Top, Math.Max(0, textRight - textLeft), tabRect.Height);

                TextRenderer.DrawText(e.Graphics, page.Text, tabControl.Font, textRect,
                    selected ? TextColor : SubtleTextColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            catch
            { }
        }

        #endregion

        #region Menu and status bar

        private static UIThemeRenderer renderer;

        private static UIThemeRenderer Renderer
        {
            get
            {
                if (renderer == null)
                    renderer = new UIThemeRenderer();

                return renderer;
            }
        }

        private static void ApplyRenderers()
        {
            try
            {
                if (!(ToolStripManager.Renderer is UIThemeRenderer))
                    ToolStripManager.Renderer = Renderer;
            }
            catch
            { }
        }

        private static void StyleToolStrip(ToolStrip _strip)
        {
            try
            {
                _strip.Renderer = Renderer;

                SetBackground(_strip, _strip is ContextMenuStrip ? Surface : MenuBackground);
                SetForeground(_strip, TextColor);

                StyleToolStripItems(_strip.Items);
            }
            catch
            { }
        }

        private static void StyleToolStripItems(ToolStripItemCollection _items)
        {
            foreach (ToolStripItem item in _items)
            {
                if (item.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb())
                    item.ForeColor = TextColor;

                ToolStripMenuItem menuItem = item as ToolStripMenuItem;

                if (menuItem == null)
                    continue;

                ToolStripDropDown dropDown = menuItem.DropDown;

                dropDown.Renderer = Renderer;
                dropDown.BackColor = Surface;
                dropDown.ForeColor = TextColor;

                StyleToolStripItems(menuItem.DropDownItems);
            }
        }

        private sealed class UIThemeColorTable : ProfessionalColorTable
        {
            public UIThemeColorTable()
            {
                UseSystemColors = false;
            }

            public override Color MenuStripGradientBegin { get { return MenuBackground; } }
            public override Color MenuStripGradientEnd { get { return MenuBackground; } }
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color MenuBorder { get { return BorderColor; } }
            public override Color MenuItemBorder { get { return AccentColor; } }
            public override Color MenuItemSelected { get { return HighlightColor; } }
            public override Color MenuItemSelectedGradientBegin { get { return HighlightColor; } }
            public override Color MenuItemSelectedGradientEnd { get { return HighlightColor; } }
            public override Color MenuItemPressedGradientBegin { get { return HighlightColor; } }
            public override Color MenuItemPressedGradientMiddle { get { return HighlightColor; } }
            public override Color MenuItemPressedGradientEnd { get { return HighlightColor; } }
            public override Color StatusStripGradientBegin { get { return MenuBackground; } }
            public override Color StatusStripGradientEnd { get { return MenuBackground; } }
            public override Color SeparatorDark { get { return BorderColor; } }
            public override Color SeparatorLight { get { return Surface; } }
            public override Color ToolStripBorder { get { return BorderColor; } }
            public override Color ToolStripGradientBegin { get { return MenuBackground; } }
            public override Color ToolStripGradientMiddle { get { return MenuBackground; } }
            public override Color ToolStripGradientEnd { get { return MenuBackground; } }
            public override Color ToolStripContentPanelGradientBegin { get { return Surface; } }
            public override Color ToolStripContentPanelGradientEnd { get { return Surface; } }
            public override Color ButtonSelectedHighlight { get { return HighlightColor; } }
            public override Color ButtonSelectedBorder { get { return BorderColor; } }
            public override Color ButtonPressedHighlight { get { return HighlightColor; } }
            public override Color ButtonCheckedHighlight { get { return HighlightColor; } }
            public override Color ButtonCheckedGradientBegin { get { return HighlightColor; } }
            public override Color ButtonCheckedGradientEnd { get { return HighlightColor; } }
            public override Color CheckBackground { get { return AccentColor; } }
            public override Color CheckSelectedBackground { get { return AccentColor; } }
            public override Color CheckPressedBackground { get { return AccentColor; } }
            public override Color GripDark { get { return BorderColor; } }
            public override Color GripLight { get { return Surface; } }
            public override Color OverflowButtonGradientBegin { get { return Surface; } }
            public override Color OverflowButtonGradientMiddle { get { return Surface; } }
            public override Color OverflowButtonGradientEnd { get { return Surface; } }
        }

        private sealed class UIThemeRenderer : ToolStripProfessionalRenderer
        {
            public UIThemeRenderer()
                : base(new UIThemeColorTable())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? TextColor : DisabledTextColor;

                base.OnRenderItemText(e);
            }
        }

        #endregion

        #region Window title bar

        [DllImport("dwmapi.dll", ExactSpelling = true)]
        private static extern int DwmSetWindowAttribute(IntPtr _window, int _attribute, ref int _value, int _size);

        private static void ApplyTitleBar(Form _form)
        {
            try
            {
                if (_form.IsHandleCreated)
                    SetImmersiveDarkMode(_form.Handle);
                else
                {
                    _form.HandleCreated -= FormHandleCreated;
                    _form.HandleCreated += FormHandleCreated;
                }
            }
            catch
            { }
        }

        private static void FormHandleCreated(object sender, EventArgs e)
        {
            Form form = sender as Form;

            if (form != null)
                SetImmersiveDarkMode(form.Handle);
        }

        private static void SetImmersiveDarkMode(IntPtr _handle)
        {
            try
            {
                int dark = IsDark ? 1 : 0;

                if (DwmSetWindowAttribute(_handle, 20, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(_handle, 19, ref dark, sizeof(int));
            }
            catch
            { }
        }

        #endregion
    }
}
