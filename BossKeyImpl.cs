using EventHook.Hooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wbooru;
using Wbooru.Settings;

namespace WbooruPlugin.BossKey
{
    public class BossKeyImpl
    {
        internal static BossKeyImpl CurrentImpl { get; private set; }

        private KeyboardWatcher keyboard_watcher;

        public BossKeyImpl()
        {
            CurrentImpl = this;

            keyboard_watcher = new KeyboardWatcher(new SyncFactory());

            keyboard_watcher.OnKeyInput += Keyboard_watcher_OnKeyInput;

            var setting = SettingManager.LoadSetting<BossKeySetting>();

            UpdateTriggerKeys(setting.HotKeys);

            setting.PropertyChanged += (_, __) => UpdateTriggerKeys(setting.HotKeys);
        }

        private void UpdateTriggerKeys(string[] keys)
        {
            Log.Info("Rebuild trigger key: " + string.Join(" + ", keys));

            boss_key_trigger.Clear();

            foreach (var item in keys)
                boss_key_trigger[item] = false;
        }

        private Dictionary<string, bool> boss_key_trigger=new Dictionary<string, bool>();

        public void Start()
        {
            keyboard_watcher?.Start();

            Log.Info("Start hook keyboard.", "BossKeyImpl");
        }

        private void Keyboard_watcher_OnKeyInput(object sender, KeyInputEventArgs e)
        {
            if (boss_key_trigger.ContainsKey(e.KeyData.Keyname))
                boss_key_trigger[e.KeyData.Keyname] = e.KeyData.EventType == KeyEvent.down;

            CheckTrigger();
        }

        private void CheckTrigger()
        {
            if ((!boss_key_trigger.All(x => x.Value)) || boss_key_trigger.Count==0)
                return;

            ShowOrHideMainWindow();

            foreach (var pair in boss_key_trigger.ToArray())
                boss_key_trigger[pair.Key] = false;
        }

        private void ShowOrHideMainWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!(Application.Current.MainWindow is Window window))
                    return;

                window.Visibility = window.Visibility switch
                {
                    Visibility.Visible => Visibility.Hidden,
                    Visibility.Hidden => Visibility.Visible,
                };
            });
        }

        public void Stop()
        {
            keyboard_watcher?.Stop();

            Log.Info("Stop hook keyboard.", "BossKeyImpl");
        }
    }
}
