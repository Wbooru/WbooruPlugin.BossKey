using EventHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wbooru;

namespace WbooruPlugin.BossKey.UI
{
    /// <summary>
    /// ConfigHotKeyWinodw.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigHotKeyWinodw : Window
    {
        public string HotKeyDisplay
        {
            get { return (string)GetValue(HotKeyDisplayProperty); }
            set { SetValue(HotKeyDisplayProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HotKeyDisplay.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HotKeyDisplayProperty =
            DependencyProperty.Register("HotKeyDisplay", typeof(string), typeof(ConfigHotKeyWinodw), new PropertyMetadata(""));

        private HashSet<string> trigger_keys { get; } = new HashSet<string>();
        private KeyboardWatcher keyboard_watcher;

        private bool vaild_close = false;

        private bool active = true;

        public HashSet<string> TriggerKeys => vaild_close ? trigger_keys : null;

        public ConfigHotKeyWinodw()
        {
            InitializeComponent();

            using var factory = new EventHookFactory();

            keyboard_watcher = factory.GetKeyboardWatcher();
            keyboard_watcher.OnKeyInput += Keyboard_watcher_OnKeyInput;

            BossKeyImpl.CurrentImpl.Stop();
            keyboard_watcher.Start();

            Activated += (_, __) => active = true;
            Deactivated += (_, __) => active = false;
        }

        private void Keyboard_watcher_OnKeyInput(object sender, KeyInputEventArgs e)
        {
            if (e.KeyData.EventType != KeyEvent.down || !active)
                return;

            trigger_keys.Add(e.KeyData.Keyname);

            UpdateDisplay();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            trigger_keys.Clear();
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            Dispatcher.Invoke(() =>
            {
                if (trigger_keys.Count == 0)
                    HotKeyDisplay = "等待输入....";
                else
                    HotKeyDisplay = string.Join(" + ", trigger_keys);
            });
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            keyboard_watcher.Stop();
            BossKeyImpl.CurrentImpl.Start();
            vaild_close = true;
        }
    }
}
