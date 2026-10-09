using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Controls;
using BA63Driver.Interfaces;
using BA63Driver.Mapper;
using GameReaderCommon;
using Newtonsoft.Json.Linq;
using SimHub.Plugins;
using SimHub.Plugins.Devices;
using SimHub.Plugins.Devices.DeviceExtensions;
using SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules;
using SimHub.Plugins.OutputPlugins.GraphicalDash.PSE;

namespace User.SimagicBridge {
    public static class NativeGtNeo {
        public const string DeviceTypeId = "67A11361-13EE-45AE-A461-3E19E14B8F87";
        public static volatile bool Enabled;
        public static volatile bool Connected;
        public static volatile string Status = "Waiting for the existing Simagic GT Neo device.";
        public static DateTime TestUntil;
        // Same logical channel order as SimHub 9.13.2's GT Neo USB manager.
        // Ten keys, four 12-LED encoder rings, RPM strip, four auxiliary lights.
        public static IPhysicalMapper CreateMapper() {
            var maps = new List<IMap>();
            for(int i=0;i<10;i++) maps.Add(new ButtonGroupMap(i,1));
            for(int i=10;i<14;i++) maps.Add(new ButtonGroupMap(i,12));
            maps.Add(new LedRangeMap(0,15));
            for(int i=14;i<18;i++) maps.Add(new ButtonGroupMap(i,1));
            return new PhysicalMapper(maps.ToArray());
        }
    }
    public sealed class NativeGtNeoManager : LedsGenericManager<ILedButtonsDriver>, ILedDeviceManager {
        public const int HeartbeatTimeoutMilliseconds = 5000;
        bool ILedDeviceManager.IsConnected() {
            if(!NativeGtNeo.Enabled) { Close();NativeGtNeo.Connected=false;return false; }
            bool connected=base.IsConnected();
            NativeGtNeo.Connected=connected;
            return connected;
        }
        public override int ForceRefreshDelaySeconds { get { return 1; } }
        public override double ReConnectIntervalSeconds { get { return 1; } }
        public override ILedButtonsDriver GetDriver() {
            if(!NativeGtNeo.Enabled) return null;
            try {
                var mapper = NativeGtNeo.CreateMapper();
                // The installed SDK exposes this factory internally. Restrict the
                // adapter to its exact signature; do not construct HID reports.
                var type = typeof(BA63Driver.SimagicGTNeoDriver).Assembly.GetType("BA63Driver.SimagicBaseWheelDriver",true);
                var method = type.GetMethod("GetDevice",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,
                    null,new[]{typeof(IPhysicalMapper),typeof(int),typeof(int),typeof(int)},null);
                if(method == null) throw new NotSupportedException("The SimHub wheelbase driver is unavailable. Check your SimHub version.");
                var driver = method.Invoke(null,new object[]{mapper,0x0805,mapper.GetPhysicalLedCount(),HeartbeatTimeoutMilliseconds}) as ILedButtonsDriver;
                NativeGtNeo.Connected = driver != null;
                NativeGtNeo.Status = driver == null ? "GT Neo not yet detected through the wheelbase." : "Simagic GT Neo: direct LED connection through the wheelbase.";
                if(driver != null) SimHub.Logging.Current.Info("[SimagicBridge] Native GT Neo base driver connected; heartbeat timeout="+HeartbeatTimeoutMilliseconds+" ms; original profiles retained.");
                return driver;
            } catch(Exception ex) {
                NativeGtNeo.Connected = false;
                NativeGtNeo.Status = "Wheelbase connection failed: " + (ex.InnerException ?? ex).Message;
                SimHub.Logging.Current.Warn("[SimagicBridge] " + NativeGtNeo.Status);
                return null;
            }
        }
        void ILedDeviceManager.Display(Func<Color[]> leds, Func<Color[]> buttons, Func<Color[]> encoders, Func<Color[]> matrix,
            Func<Color[]> rawState, Func<Color[]> overrideState, bool forceRefresh, Func<object> extraData,
            double rpmBrightness,double buttonsBrightness,double encodersBrightness,double matrixBrightness) {
            if(!NativeGtNeo.Enabled) { Close(); NativeGtNeo.Connected=false; return; }
            if(DateTime.UtcNow < NativeGtNeo.TestUntil) {
                var original = buttons;
                buttons = () => { var colors=original==null?new Color[18]:(Color[])original().Clone(); if(colors.Length>0)colors[0]=Color.Cyan;return colors; };
                forceRefresh=true;
            }
            base.Display(leds,buttons,encoders,matrix,rawState,overrideState,forceRefresh,extraData,rpmBrightness,buttonsBrightness,encodersBrightness,matrixBrightness);
        }
    }
    public sealed class NativeGtNeoExtensionFilter : IDeviceExtensionFilter {
        public IEnumerable<Type> GetExtensionsTypes(DeviceInstance device) {
            if(device.DeviceDescriptor != null && String.Equals(device.DeviceDescriptor.DeviceTypeID,NativeGtNeo.DeviceTypeId,StringComparison.OrdinalIgnoreCase))
                yield return typeof(NativeGtNeoExtension);
        }
    }
    public sealed class NativeGtNeoExtension : DeviceExtension {
        private LedModuleSettings settings;
        private ILedDeviceManager previous;
        private NativeGtNeoManager replacement;
        public override string ExtentionTabTitle { get { return "Wheelbase connection"; } }
        public override void Init(PluginManager manager) {
            if(LinkedDevice.DeviceDescriptor == null || !String.Equals(LinkedDevice.DeviceDescriptor.DeviceTypeID,NativeGtNeo.DeviceTypeId,StringComparison.OrdinalIgnoreCase)) return;
            var device = LinkedDevice as LedModuleDevice;
            if(device == null) throw new NotSupportedException("The GT Neo device has no LED settings.");
            var field=typeof(LedModuleDevice).GetField("ledModuleSettings",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            settings=field==null?null:field.GetValue(device) as LedModuleSettings;
            if(settings == null) throw new NotSupportedException("Unable to attach to SimHub LED settings. Check your SimHub version.");
            if(settings.DeviceDriver is NativeGtNeoManager) return;
            previous=settings.DeviceDriver;
            previous.Close();
            replacement=new NativeGtNeoManager { LedModuleSettings=settings };
            SetDriver(replacement);
            SimHub.Logging.Current.Info("[SimagicBridge] Attached base transport to existing Simagic GT Neo. Profiles and car overrides unchanged.");
        }
        public override void End(PluginManager manager) {
            // Only the extension that replaced the original manager owns its
            // lifetime. A second SDK initialization must not close that owner.
            if(replacement==null) return;
            if(replacement!=null) replacement.Close();
            if(settings!=null && settings.DeviceDriver==replacement) SetDriver(previous);
            NativeGtNeo.Connected=false;
        }
        public override void DataUpdate(PluginManager manager,ref GameData data) {
            if(!NativeGtNeo.Enabled && replacement!=null) { replacement.Close();NativeGtNeo.Connected=false; }
        }
        public override void ConnectionStatusChanged(DeviceState state) { }
        public override void LoadDefaultSettings() { }
        public override JToken GetSettings() { return new JObject(); }
        public override void SetSettings(JToken data,bool loadDefaults) { }
        private void SetDriver(ILedDeviceManager driver) {
            typeof(LedModuleSettings).GetProperty("DeviceDriver").GetSetMethod(true).Invoke(settings,new object[]{driver});
        }
        public override IEnumerable<DynamicButtonAction> GetDynamicButtonActions() { return new DynamicButtonAction[0]; }
        public override Control CreateSettingControl() {
            return new UserControl {Content=new TextBlock {Text="Simagic Bridge uses the wheelbase connection here. Configure LED profiles and car overrides in the existing device tabs.",TextWrapping=System.Windows.TextWrapping.Wrap,Margin=new System.Windows.Thickness(24)}};
        }
    }
}
