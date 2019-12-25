using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wbooru.Settings;
using Wbooru.Settings.UIAttributes;
using WbooruPlugin.BossKey.UI;

namespace WbooruPlugin.BossKey
{
    [Export(typeof(SettingBase))]
    public class BossKeySetting:SettingBase , INotifyPropertyChanged
    {
        private string[] hot_keys = new string[] { "LeftAlt", "LeftCtrl", "Q" };

        [Ignore]
        [JsonProperty]
        public string[] HotKeys
        {
            get => hot_keys;
            set
            {
                hot_keys = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HotKeys)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [CustomUI]
        public UIElement CreateHotKeyInputUI()
        {
            return new ConfigBossKeyEnterControl();
        }
    }
}
