using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wbooru.PluginExt;

namespace WbooruPlugin.BossKey
{
    [Export(typeof(PluginInfo))]
    public class BossKeyPluginInfo : PluginInfo
    {
        private BossKeyImpl impl;

        public BossKeyPluginInfo()
        {
            if (impl == null)
            {
                impl = new BossKeyImpl();
                impl.Start();
            }
        }

        public override string PluginName => "BossKey";

        public override string PluginProjectWebsite => "https://github.com/MikiraSora/WbooruPlugin.BossKey";

        public override string PluginAuthor => "DarkProjector";

        public override string PluginDescription => "WTMSB.";

        protected override void OnApplicationTerm()
        {
            base.OnApplicationTerm();

            impl?.Stop();
        }
    }
}
