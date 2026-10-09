using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json.Linq;
using BA63Driver.Interfaces;
using BA63Driver.Mapper;
using SerialDash;
using SimHub.Plugins.Devices;
using SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules;
using SimHub.Plugins.OutputPlugins.GraphicalDash.PSE;

namespace User.SimagicBridge {
    public static class BridgeOutput {
        public static BridgeController Controller;
    }
    // Device registration is intentionally isolated from the transport. SimHub's device interfaces are versioned.
    public sealed class GtNeoBridgeRegistry : IDeviceDescriptorsRegistry {
        public const string DeviceId = "231B4B8C-A383-490A-8551-916D330F1437";
        public IEnumerable<DeviceDescriptor> GetDevices() {
            var result = new List<DeviceDescriptor>();
            try {
                result.Add(new DeviceDescriptor {
                    DeviceTypeID = DeviceId, Name = "GT Neo via SimPro", Brand = "Simagic Bridge", MaximumInstances = 1,
                    AllowParentDeviceSettingsInheritence = false, Factory = CreateDevice
                });
            } catch(Exception ex) { SimHub.Logging.Current.Warn("[SimagicBridge] Device registration: " + ex.Message); }
            return result;
        }
        public static DeviceInstance CreateDevice() {
            return new GtNeoBridgeDevice(new LedModuleSettings<GtNeoBridgeDriver>(new LedModuleOptions {
                DeviceName = "GT Neo via SimPro", LedCount = 15, ButtonsCount = 10, EncodersCount = 4,
                DefaultButtonsColor = Enumerable.Repeat(Color.White, 10).ToArray(),
                DefaultEncodersColor = Enumerable.Repeat(Color.White, 4).ToArray(),
                RawLedCount = 29, TelemetryLedsLabel = "RPM strip", ButtonsColorLabel = "Buttons",
                EncodersColorLabel = "Encoder", LedDriver = new GtNeoBridgeDriver(), ShowConnectionStatus = true,
                EnableBrightnessSection = true
            }));
        }
    }
    public sealed class GtNeoBridgeDevice : LedModuleDevice {
        public GtNeoBridgeDevice(LedModuleSettings settings) : base(settings) { }
        public override void SetSettings(JToken settings, bool loadDefaults) {
            base.SetSettings(BridgeDefaultProfiles.Prepare(settings), loadDefaults);
        }
        public override void LoadDefaultSettings() {
            base.LoadDefaultSettings();
            base.SetSettings(BridgeDefaultProfiles.Prepare(base.GetSettings(false, false)), true);
        }
    }
    public sealed class GtNeoBridgeDriver : ILedDeviceManager {
        public LedModuleSettings LedModuleSettings { get; set; }
        public LedDeviceState LastState { get; private set; }
        public event EventHandler BeforeDisplay, AfterDisplay, OnConnect, OnDisconnect;
#pragma warning disable 67
        public event EventHandler OnError;
#pragma warning restore 67
        private bool wasConnected;
        public void Display(Func<Color[]> leds, Func<Color[]> buttons, Func<Color[]> encoders, Func<Color[]> matrix,
                            Func<Color[]> rawState, Func<Color[]> overrideState, bool forceRefresh, Func<object> extraData = null,
                            double rpmBrightness = 1, double buttonsBrightness = 1, double encodersBrightness = 1, double matrixBrightness = 1) {
            if(BeforeDisplay != null) BeforeDisplay(this, EventArgs.Empty);
            var rpm = leds == null ? new Color[0] : leds();
            var keys = buttons == null ? new Color[0] : buttons();
            var knobs = encoders == null ? new Color[0] : encoders();
            var raw = rawState == null ? new Color[0] : rawState();
            var overrides = overrideState == null ? new Color[0] : overrideState();
            LastState = new LedDeviceState(rpm, keys, knobs, new Color[0], raw, overrides, rpmBrightness, buttonsBrightness, encodersBrightness, matrixBrightness);
            var controller = BridgeOutput.Controller;
            if(controller != null && controller.Enabled) controller.Publish(LightFrame.FromGroups(rpm, keys, knobs, raw, rpmBrightness, buttonsBrightness, encodersBrightness, overrides));
            if(AfterDisplay != null) AfterDisplay(this, EventArgs.Empty);
        }
        public bool IsConnected() {
            var controller = BridgeOutput.Controller;
            bool connected = controller != null && controller.Enabled && controller.Connected;
            if(connected != wasConnected) {
                wasConnected = connected;
                var handler = connected ? OnConnect : OnDisconnect;
                if(handler != null) handler(this, EventArgs.Empty);
            }
            return connected;
        }
        public string GetSerialNumber() { return "SimPro-GTNeo"; }
        public string GetFirmwareVersion() { var c=BridgeOutput.Controller; return c == null ? "" : c.Firmware; }
        public object GetDriverInstance() { return this; }
        public void Close() { wasConnected = false; }
        public void ResetDetection() { }
        public void SerialPortCanBeScanned(object sender, SerialDashController.ScanArgs args) { }
        public IPhysicalMapper GetPhysicalMapper() { return null; }
        public ILedDriverBase GetLedDriver() { return null; }
    }
}
