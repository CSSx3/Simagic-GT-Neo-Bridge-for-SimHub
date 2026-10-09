using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using GameReaderCommon;
using SimHub.Plugins;

namespace User.SimagicBridge {
    public sealed class BridgeSettings { public bool Enabled = false; public bool NativeMode = true; }
    [PluginName("Simagic Bridge")]
    [PluginAuthor("Nico")]
    [PluginDescription("Controls GT Neo lights through the existing wheelbase connection using SimHub LED profiles.")]
    public sealed class SimagicBridgePlugin : IPlugin, IDataPlugin, IWPFSettingsV2 {
        public PluginManager PluginManager { get; set; }
        public ImageSource PictureIcon { get { return null; } }
        public string LeftMenuTitle { get { return "Simagic Bridge"; } }
        public BridgeController Controller { get; private set; }
        public BridgeSettings Settings { get; private set; }
        public static string JournalPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimagicSimHubBridge", "restore-lights.json"); } }
        public void Init(PluginManager pluginManager) {
            PluginManager = pluginManager;
            Settings = this.ReadCommonSettings<BridgeSettings>("Settings", () => new BridgeSettings());
            Controller = new BridgeController(new SimProClient(), JournalPath);
            Controller.Enabled = Settings.Enabled && !Settings.NativeMode;
            NativeGtNeo.Enabled = Settings.Enabled && Settings.NativeMode;
            BridgeOutput.Controller = Controller;
            Controller.Start();
            this.AttachDelegate("Status", () => Settings.NativeMode ? NativeGtNeo.Status : Controller.Status);
            this.AttachDelegate("Connected", () => Settings.NativeMode ? NativeGtNeo.Connected : Controller.Connected);
            this.AttachDelegate("Enabled", () => Settings.Enabled);
            this.AttachDelegate("RecoveryPending", () => Controller.RecoveryPending);
            this.AttachDelegate("RpcMilliseconds", () => Controller.Latency);
            this.AttachDelegate("OutputWrites", () => Controller.Writes);
            this.AddAction("Start", (a,b) => SetEnabled(true));
            this.AddAction("Stop", (a,b) => SetEnabled(false));
            this.AddAction("TestLight", (a,b) => TestLight());
            this.AddAction("RestoreLights", (a,b) => RestoreLights());
            SimHub.Logging.Current.Info("[SimagicBridge] Started. Restore journal: " + JournalPath);
        }
        public void SetEnabled(bool enabled) {
            Settings.Enabled = enabled; Controller.Enabled = enabled && !Settings.NativeMode;
            NativeGtNeo.Enabled = enabled && Settings.NativeMode; Save();
        }
        public void TestLight() {
            if(Settings.NativeMode) { NativeGtNeo.TestUntil=DateTime.UtcNow.AddSeconds(3); }
            else { Settings.Enabled=false;Controller.RequestTest();Save(); }
        }
        public void RestoreLights() { SetEnabled(false);Controller.RequestRestore(); }
        public void Save() { this.SaveCommonSettings("Settings", Settings); }
        public void DataUpdate(PluginManager pluginManager, ref GameData data) { }
        public Control GetWPFSettingsControl(PluginManager pluginManager) { return new BridgeSettingsControl(this); }
        public void End(PluginManager pluginManager) {
            if(Controller != null) {
                if(!Settings.NativeMode) Settings.Enabled = Controller.Enabled;
                NativeGtNeo.Enabled=false;
                BridgeOutput.Controller = null;
                Controller.Dispose(); Save();
                if(Controller.RecoveryPending) SimHub.Logging.Current.Warn("[SimagicBridge] Restore pending: " + Controller.Error);
                else SimHub.Logging.Current.Info("[SimagicBridge] Stopped; original lights restored.");
            }
        }
    }
}
