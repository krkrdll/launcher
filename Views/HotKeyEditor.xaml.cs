using Launcher.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Launcher
{
    /// <summary>
    /// Reusable Ctrl/Shift/Alt/Win + key editor for configuring a single hotkey, with built-in
    /// "at least one modifier" validation. Drop another instance into the settings UI to add a
    /// new configurable shortcut.
    /// </summary>
    public sealed partial class HotKeyEditor : UserControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(HotKeyEditor), new PropertyMetadata(string.Empty, OnTitleChanged));

        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
            nameof(Description), typeof(string), typeof(HotKeyEditor), new PropertyMetadata(string.Empty, OnDescriptionChanged));

        public HotKeyEditor()
        {
            InitializeComponent();

            for (var key = 'A'; key <= 'Z'; key++)
            {
                KeyComboBox.Items.Add(key.ToString());
            }
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>Populates the editor from a saved hotkey and clears any validation error.</summary>
        public void SetHotKey(HotKeyDefinition hotKey)
        {
            CtrlCheckBox.IsChecked = hotKey.ModifierCtrl;
            ShiftCheckBox.IsChecked = hotKey.ModifierShift;
            AltCheckBox.IsChecked = hotKey.ModifierAlt;
            WinCheckBox.IsChecked = hotKey.ModifierWin;
            KeyComboBox.SelectedItem = hotKey.Key.ToString();
            ValidationMessage.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Reads the currently configured hotkey. Returns false and shows a validation message
        /// when no modifier key is selected.
        /// </summary>
        public bool TryGetHotKey(out HotKeyDefinition hotKey)
        {
            var candidate = new HotKeyDefinition
            {
                ModifierCtrl = CtrlCheckBox.IsChecked ?? false,
                ModifierShift = ShiftCheckBox.IsChecked ?? false,
                ModifierAlt = AltCheckBox.IsChecked ?? false,
                ModifierWin = WinCheckBox.IsChecked ?? false,
            };

            if (!candidate.HasModifier || KeyComboBox.SelectedItem is not string keyText)
            {
                ValidationMessage.Visibility = Visibility.Visible;
                hotKey = candidate;
                return false;
            }

            ValidationMessage.Visibility = Visibility.Collapsed;
            candidate.Key = keyText[0];
            hotKey = candidate;
            return true;
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((HotKeyEditor)d).TitleTextBlock.Text = (string)e.NewValue;

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((HotKeyEditor)d).DescriptionTextBlock.Text = (string)e.NewValue;
    }
}
