using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using User.SimagicBridge;

class BridgeTests {
    private static int assertions;
    private static string root;
    private static string simHubPath;
    [STAThread] static int Main(string[] args) {
        root = args[0];
        simHubPath = args[1];
        Directory.SetCurrentDirectory(simHubPath);
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
            string path=Path.Combine(simHubPath,new AssemblyName(e.Name).Name+".dll");
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        try { Run(args.Skip(2).ToArray()); Console.WriteLine("PASS " + assertions + " assertions"); return 0; }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Check(bool pass,string name) { assertions++; if(!pass) throw new Exception("FAIL " + name); }
    [MethodImpl(MethodImplOptions.NoInlining)] static void Run(string[] args) {
        var fixture = JObject.Parse(File.ReadAllText(Path.Combine(root,"tests/fixtures/gt-neo-1.4.4.json")));
        if(args.Contains("--live")) { Live().GetAwaiter().GetResult(); return; }
        if(args.Contains("--preview")) { Preview(); return; }
        TestCore(fixture).GetAwaiter().GetResult();
        var device=GtNeoBridgeRegistry.CreateDevice();
        Check(device != null,"SimHub device factory");
        var driver=new GtNeoBridgeDriver();
        driver.Display(()=>Enumerable.Repeat(Color.Green,15).ToArray(),()=>Enumerable.Repeat(Color.White,10).ToArray(),()=>Enumerable.Repeat(Color.Blue,4).ToArray(),null,null,()=>new[]{Color.Red},false);
        Check(driver.LastState.LedsState.Length==15,"SimHub LED pipeline");
        Check(driver.LastState.OverrideState[0]==Color.Red,"SimHub 9.13 override channel accepted");
        Check(!driver.IsConnected(),"no false hardware connection");
        var mapper=NativeGtNeo.CreateMapper();
        Check(mapper.GetPhysicalLedCount()==77,"native GT Neo mapping includes rings and auxiliary lights");
        var nativeState=new BA63Driver.Mapper.LedDeviceState(Enumerable.Repeat(Color.Green,15).ToArray(),Enumerable.Range(0,18).Select(i=>i==0?Color.Cyan:i==10?Color.Blue:Color.Red).ToArray(),new Color[0],new Color[0],new Color[0],new Color[0],1,1,1,1);
        Check(mapper.GetColor(0,nativeState,false).ToArgb()==Color.Cyan.ToArgb(),"native first button uses original button channel");
        Check(Enumerable.Range(10,12).All(i=>mapper.GetColor(i,nativeState,false).ToArgb()==Color.Blue.ToArgb()),"native encoder ring retains all twelve physical LEDs");
        Check(mapper.GetColor(58,nativeState,false).ToArgb()==Color.Green.ToArgb(),"native RPM strip uses original physical offset");
        var originalSettings=device.GetSettings(false,false).DeepClone();
        // Connecting a manager populates these lazy SDK brightness caches.
        // The public brightness values and all profiles must remain identical.
        foreach(string group in new[]{"LEDs","Buttons","Encoders"})
            originalSettings["ledModuleSettings"]["_"+group+"Brightness"]=originalSettings["ledModuleSettings"][group+"Brightness"].DeepClone();
        var extension=new NativeGtNeoExtension();
        device.DeviceDescriptor=new SimHub.Plugins.Devices.DeviceDescriptor {DeviceTypeID=NativeGtNeo.DeviceTypeId};
        typeof(SimHub.Plugins.Devices.DeviceExtensions.DeviceExtension).GetProperty("LinkedDevice").GetSetMethod(true).Invoke(extension,new object[]{device});
        extension.Init(null);
        Check(JToken.DeepEquals(originalSettings,device.GetSettings(false,false)),"transport replacement preserves all existing profiles and settings");
        var settingsField=typeof(SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules.LedModuleDevice).GetField("ledModuleSettings",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        var linkedSettings=(SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules.LedModuleSettings)settingsField.GetValue(device);
        var owner=linkedSettings.DeviceDriver;
        var secondExtension=new NativeGtNeoExtension();
        typeof(SimHub.Plugins.Devices.DeviceExtensions.DeviceExtension).GetProperty("LinkedDevice").GetSetMethod(true).Invoke(secondExtension,new object[]{device});
        secondExtension.Init(null);
        secondExtension.End(null);
        Check(Object.ReferenceEquals(owner,linkedSettings.DeviceDriver),"duplicate SDK extension initialization does not replace or close the owning manager");
        extension.End(null);
        Check(JToken.DeepEquals(originalSettings,device.GetSettings(false,false)),"closing extension restores manager without rewriting profiles");
        var foreignDevice=GtNeoBridgeRegistry.CreateDevice();
        foreignDevice.DeviceDescriptor=new SimHub.Plugins.Devices.DeviceDescriptor {DeviceTypeID=GtNeoBridgeRegistry.DeviceId};
        var foreignSettings=(SimHub.Plugins.OutputPlugins.GraphicalDash.LedModules.LedModuleSettings)settingsField.GetValue(foreignDevice);
        var foreignDriver=foreignSettings.DeviceDriver;
        var foreignExtension=new NativeGtNeoExtension();
        typeof(SimHub.Plugins.Devices.DeviceExtensions.DeviceExtension).GetProperty("LinkedDevice").GetSetMethod(true).Invoke(foreignExtension,new object[]{foreignDevice});
        foreignExtension.Init(null); foreignExtension.End(null);
        Check(Object.ReferenceEquals(foreignDriver,foreignSettings.DeviceDriver),"native extension never attaches to the separate legacy bridge device");
    }
    static async Task TestCore(JObject fixture) {
        var button=(JObject)fixture["config"]["led_buttons"]["1"];
        var before=button.ToString(); var patch=PresetPatches.Button(button,Color.Cyan);
        Check(button.ToString()==before,"patch does not mutate original");
        Check(JToken.DeepEquals(button["button"],patch["button"]),"button code preserved");
        Check((string)patch["led"]["color"]=="#00FFFF","button colour");
        Check(((JArray)patch["led"]["items"]).OfType<JObject>().Count(x=>(bool)x["enabled"])==1,"only static light event owns button");
        Check(((JArray)patch["led"]["items"]).OfType<JObject>().Select(x=>(int)x["index"]).SequenceEqual(Enumerable.Range(1,6)),"duplicate stored light indices are normalized for SimPro");
        var encoder=(JObject)fixture["config"]["encoders"]["1"];
        var encoderPatch=PresetPatches.Encoder(encoder,Color.Red);
        Check(JToken.DeepEquals(encoder["tuner"],encoderPatch["tuner"]),"encoder input preserved");
        Check(JToken.DeepEquals(encoder["is_abs_mode"],encoderPatch["is_abs_mode"]),"absolute encoder mode preserved");
        Check(((JArray)encoderPatch["light_group"]["items"]).OfType<JObject>().Select(x=>(int)x["index"]).SequenceEqual(Enumerable.Range(1,6)),"encoder events use unique indices");
        var frame=LightFrame.FromGroups(new[]{Color.FromArgb(128,100,200,50)},new[]{Color.White},new[]{Color.Blue},new[]{Color.Black},.5,0,Double.NaN);
        Check(frame.Rpm[0].ToArgb()==Color.Black.ToArgb(),"opaque raw black overrides light");
        Check(frame.Buttons[0].ToArgb()==Color.Black.ToArgb(),"zero brightness turns off button");
        Check(frame.Encoders[0].ToArgb()==Color.Black.ToArgb(),"non-finite brightness clamped");
        var source=new[]{Color.Red}; var copied=new LightFrame(source,null,null); source[0]=Color.Blue;
        Check(copied.Rpm[0]==Color.Red,"frame owns its arrays");
        var overridden=LightFrame.FromGroups(new[]{Color.Green},null,null,new[]{Color.Blue},1,1,1,new[]{Color.Black});
        Check(overridden.Rpm[0].ToArgb()==Color.Black.ToArgb(),"SimHub overrides have priority over raw and regular profiles, including black");
        var untouched=JObject.Parse("{\"ledModuleSettings\":{},\"leds\":{\"Profiles\":[{\"Name\":\"Default Profile\",\"CarChoices\":[],\"LedContainers\":[]}]}}");
        var seeded=BridgeDefaultProfiles.Prepare(untouched);
        Check((int)seeded["leds"]["Profiles"][0]["LedContainers"][0]["LedContainers"][0]["LedCount"]==15,"new empty profile gains 15 RPM LEDs");
        var nativeProfile=seeded["leds"]["Profiles"][0].ToObject<SimHub.Plugins.DataPlugins.RGBDriver.Settings.Profile>();
        Check(nativeProfile.LedContainers.Count==1 && nativeProfile.LedContainers[0].GetType().Name=="GameRunningGroupContainer","native SimHub profile converter loads seeded effect groups");
        Check(!((JArray)untouched["leds"]["Profiles"][0]["LedContainers"]).Any(),"profile migration does not mutate input settings");
        Check((bool)seeded["ledModuleSettings"]["ForceRefreshWithNoChanges"],"static profiles keep the bridge heartbeat alive");
        Check(JToken.DeepEquals(seeded,BridgeDefaultProfiles.Prepare(seeded)),"default migration runs only once");
        ((JArray)untouched["leds"]["Profiles"]).Add(JObject.Parse("{\"Name\":\"Car override\",\"LedContainers\":[{\"ContainerType\":\"StaticColor\",\"Color\":\"Blue\"}]}"));
        Check(JToken.DeepEquals(untouched["leds"],BridgeDefaultProfiles.Prepare(untouched)["leds"]),"existing effects and car overrides are preserved");
        var rpm=PresetPatches.Rpm((JObject)fixture["config"]["rpm_lights"]["1"],Enumerable.Repeat(Color.Green,15).ToArray());
        Check(((JArray)rpm["lights"][0]["value"]).Values<int>().All(v=>v==200),"RPM uses nonzero activation threshold");
        string folder=Path.Combine(root,"bin/test-journals"); Directory.CreateDirectory(folder);
        string journal=Path.Combine(folder,Guid.NewGuid()+".json");
        var fake=new FakeClient(fixture); var session=new BridgeSession(fake,journal);
        await session.ConnectAsync(); await session.BeginAsync();
        Check(File.Exists(journal) && fake.Writes.Count==0,"journal saved before writes");
        var all=new LightFrame(Enumerable.Repeat(Color.Green,15).ToArray(),Enumerable.Repeat(Color.Cyan,10).ToArray(),Enumerable.Repeat(Color.Yellow,4).ToArray());
        await session.ApplyAsync(all,()=>true); Check(fake.Writes.Count==15,"all light groups written");
        Check(fake.Writes.All(x=>new[]{"led_buttons","encoders","rpm_lights"}.Contains((string)x["part_type"])),"only wheel lights written");
        Check(JToken.DeepEquals(fixture["config"]["paddle_groups"],fake.Current["config"]["paddle_groups"]),"clutch and paddles untouched");
        await session.ApplyAsync(all,()=>true); Check(fake.Writes.Count==15,"identical frames suppressed");
        await session.RestoreAsync(); Check(JToken.DeepEquals(fixture,fake.Current),"all originals restored"); Check(!File.Exists(journal),"verified restore clears journal");
        await session.BeginAsync(); fake.ThrowAfterApplying=true;
        try { await session.ApplyAsync(all,()=>true); } catch(System.Net.Http.HttpRequestException) { }
        Check(File.Exists(journal),"uncertain write retains journal"); fake.ThrowAfterApplying=false;
        var restarted=new BridgeSession(fake,journal); await restarted.ConnectAsync(); await restarted.RecoverAsync();
        Check(JToken.DeepEquals(fixture,fake.Current),"restart recovers uncertain write");
        await session.RestoreAsync(); await session.BeginAsync(); await session.ApplyAsync(all,()=>true);
        fake.Current["preset_uuid"]="different-preset"; int writes=fake.Writes.Count;
        bool refused=false; try{await session.RestoreAsync();}catch(InvalidOperationException){refused=true;}
        Check(refused && fake.Writes.Count==writes && File.Exists(journal),"different preset is never overwritten");
        fake.Current["preset_uuid"]=fixture["preset_uuid"].DeepClone(); await session.RestoreAsync();
        fake.DuplicateWheel=true; bool duplicate=false; try{await session.ConnectAsync();}catch(InvalidOperationException){duplicate=true;}
        Check(duplicate,"ambiguous wheel detection refuses output");
        var queued=new FakeClient(fixture) { CommitDelayReads = 2 };
        var sequenced=new BridgeSession(queued,journal); await sequenced.ConnectAsync(); await sequenced.BeginAsync();
        await sequenced.ApplyAsync(all,()=>true);
        Check(queued.DroppedWrites==0,"queued SimPro commits never overwrite unconfirmed parts");
        Check(queued.Writes.Count==15,"queued transport commits every light group");
        await sequenced.RestoreAsync();
        Check(JToken.DeepEquals(fixture,queued.Current) && !File.Exists(journal),"queued transport restores the complete original");
    }
    static async Task Live() {
        string journal=Path.Combine(root,"diagnostics/private/plugin-live-restore.json");
        using(var client=new SimProClient()) {
            var session=new BridgeSession(client,journal); await session.ConnectAsync();
            Console.WriteLine("LIVE " + session.BaseName + " / GT Neo " + session.WheelFirmware);
            try {
                await session.BeginAsync();
                var frame=new LightFrame(Enumerable.Repeat(Color.FromArgb(0,60,0),15).ToArray(),Enumerable.Repeat(Color.FromArgb(0,60,60),10).ToArray(),Enumerable.Repeat(Color.FromArgb(60,30,0),4).ToArray());
                await session.ApplyAsync(frame,()=>true); Console.WriteLine("LIVE accepted="+session.Writes+" last_rpc_ms="+session.LastRpcMilliseconds);
                await Task.Delay(250);
                var devices=(JArray)await client.CallAsync("get_device_list",new JObject());
                var wheel=devices.OfType<JObject>().Single(x=>(string)x["product_name"]=="GT NEO");
                var identity=new JObject(); identity["device_uuid"]=wheel["device_uuid"].DeepClone(); identity["product_uuid"]=wheel["product_uuid"].DeepClone();
                var current=(JObject)await client.CallAsync("preset_get_selected_dev_config",identity);
                string actualButton=(string)current["config"]["led_buttons"]["10"]["led"]["color"];
                var staticEncoder=((JArray)current["config"]["encoders"]["4"]["light_group"]["items"]).OfType<JObject>().Single(x=>(string)x["key"]=="STATIC_EFFECT");
                string actualEncoder=(string)staticEncoder["light"]["configs"]["mono"]["foregroud_color"][0];
                string actualRpm=(string)current["config"]["rpm_lights"]["1"]["lights"][0]["color"][0];
                Console.WriteLine("LIVE values button="+actualButton+" encoder="+actualEncoder+" rpm="+actualRpm);
                Check(((JObject)current["config"]["led_buttons"]).Properties().All(p=>String.Equals((string)p.Value["led"]["color"],"#003C3C",StringComparison.OrdinalIgnoreCase)),"all real button outputs read back");
                Check(((JObject)current["config"]["encoders"]).Properties().All(p=>String.Equals((string)((JArray)p.Value["light_group"]["items"]).OfType<JObject>().Single(x=>(string)x["key"]=="STATIC_EFFECT")["light"]["configs"]["mono"]["foregroud_color"][0],"#3C1E00",StringComparison.OrdinalIgnoreCase)),"all real encoder outputs read back");
                Check(((JArray)current["config"]["rpm_lights"]["1"]["lights"][0]["color"]).Values<string>().All(c=>String.Equals(c,"#003C00",StringComparison.OrdinalIgnoreCase)),"real RPM output readback");
                Console.WriteLine("LIVE readback confirmed for buttons, encoders and RPM.");
                await Task.Delay(4000);
            } finally { session.RestoreAsync().GetAwaiter().GetResult(); }
            Check(!File.Exists(journal),"real API confirms all light groups restored");
        }
    }
    static void Preview() {
        var control=new BridgeSettingsControl(new SimagicBridgePlugin());
        var grid=new System.Windows.Controls.Grid { Width=900, Height=850, Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28,30,35)) };
        grid.Children.Add(control); grid.Measure(new System.Windows.Size(900,850)); grid.Arrange(new System.Windows.Rect(0,0,900,850)); grid.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(900,850,96,96,System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(grid);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using(var stream=File.Create(Path.Combine(root,"artifacts/plugin-preview.png"))) encoder.Save(stream);
    }
    sealed class FakeClient : ISimProClient {
        public JObject Current;
        public readonly List<JObject> Writes=new List<JObject>();
        public bool ThrowAfterApplying,DuplicateWheel;
        public int CommitDelayReads,DroppedWrites;
        private JObject pending;
        private int readsUntilCommit;
        public FakeClient(JObject fixture) { Current=(JObject)fixture.DeepClone(); }
        public Task<JToken> CallAsync(string method,JObject body) {
            if(method=="get_device_list") {
                var wheel=JObject.Parse("{\"product_type\":\"wheel\",\"product_name\":\"GT NEO\",\"firmware_version\":\"test\",\"device_uuid\":\"wheel\",\"product_uuid\":\"product\"}");
                return Task.FromResult<JToken>(DuplicateWheel?new JArray(wheel,wheel.DeepClone()):new JArray(wheel));
            }
            if(method=="preset_get_selected_dev_config") {
                if(pending!=null && --readsUntilCommit<=0) { Commit(pending); pending=null; }
                return Task.FromResult(Current.DeepClone());
            }
            if(method!="preset_set_dev_config") throw new Exception("Unknown operation");
            Writes.Add((JObject)body.DeepClone());
            if(CommitDelayReads>0) { if(pending!=null) DroppedWrites++; pending=(JObject)body.DeepClone(); readsUntilCommit=CommitDelayReads; }
            else Commit(body);
            if(ThrowAfterApplying) throw new System.Net.Http.HttpRequestException("Reply lost after device applied write.");
            return Task.FromResult<JToken>(JValue.CreateNull());
        }
        private void Commit(JObject body) { Current["config"][(string)body["part_type"]][((int)body["part_id"]).ToString()]=body["config"].DeepClone(); }
        public void Dispose() { }
    }
}
