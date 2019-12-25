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
using Wbooru.Settings;

namespace WbooruPlugin.BossKey.UI
{
    /// <summary>
    /// ConfigBossKeyEnterControl.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigBossKeyEnterControl : UserControl
    {
        public ConfigBossKeyEnterControl()
        {
            InitializeComponent();

            UpdateCurrentHotKey();
        }

        private void UpdateCurrentHotKey()
        {
            BossKeyValue.Text = string.Join(" + ", SettingManager.LoadSetting<BossKeySetting>().HotKeys);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var window = new ConfigHotKeyWinodw();

            window.ShowDialog();

            var keys = window.TriggerKeys;

            if (keys!=null && keys.Count != 0)
            {
                SettingManager.LoadSetting<BossKeySetting>().HotKeys = keys.ToArray();

                UpdateCurrentHotKey();
            }
        }
    }
}
