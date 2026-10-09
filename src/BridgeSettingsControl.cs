using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace User.SimagicBridge {
    public sealed class BridgeSettingsControl : UserControl {
        private readonly DispatcherTimer timer;
        private readonly TextBlock status;
        private readonly TextBlock details;
        private readonly TextBlock error;
        private readonly CheckBox enabled;
        private readonly SimagicBridgePlugin plugin;
        public BridgeSettingsControl(SimagicBridgePlugin plugin) {
            this.plugin = plugin;
            var stack = new StackPanel { Margin = new Thickness(28), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
            stack.Children.Add(Text("GT Neo through your wheelbase", 26, FontWeights.SemiBold));
            stack.Children.Add(Text("Control the lights through your existing wheelbase connection.", 15, FontWeights.Normal));
            status = Text("Checking connection.", 18, FontWeights.SemiBold); status.Margin = new Thickness(0,24,0,8); stack.Children.Add(status);
            details = Text("", 13, FontWeights.Normal); stack.Children.Add(details);
            error = Text("", 14, FontWeights.Normal); error.Foreground = Brushes.Orange; stack.Children.Add(error);
            enabled = new CheckBox { Content = "Control lights with SimHub", Margin = new Thickness(0,24,0,16), FontSize = 16, Foreground = Brushes.White };
            enabled.Checked += (s,e) => { if(!refreshing && plugin.Controller != null) plugin.SetEnabled(true); };
            enabled.Unchecked += (s,e) => { if(!refreshing && plugin.Controller != null) plugin.SetEnabled(false); };
            stack.Children.Add(enabled);
            var row = new WrapPanel();
            var test = new Button { Content = "Test button 1", Padding = new Thickness(16,8,16,8), Margin = new Thickness(0,0,12,0) };
            test.Click += (s,e) => plugin.TestLight();
            var restore = new Button { Content = "Restore lights", Padding = new Thickness(16,8,16,8) };
            restore.Click += (s,e) => plugin.RestoreLights();
            row.Children.Add(test); row.Children.Add(restore); stack.Children.Add(row);
            stack.Children.Add(Text("Getting started", 18, FontWeights.SemiBold));
            stack.Children.Add(Text("1. Turn on your wheelbase.\n2. Enable light control above.\n3. Enable your existing Simagic GT Neo under Devices.\n4. Use its LED profiles and car overrides.", 15, FontWeights.Normal));
            stack.Children.Add(Text("Built for the wheelbase driver in SimHub 9.13.2.\nUses the original LED mapping for buttons, encoder rings and the RPM strip.\nOther SimHub versions and wheelbases need testing.", 14, FontWeights.Normal));
            stack.Children.Add(Text("Stopping releases light control.\nThe legacy GT Neo via SimPro device is not used in native mode.", 14, FontWeights.Normal));
            Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            timer.Tick += (s,e) => Refresh();
            Loaded += (s,e) => { timer.Start(); Refresh(); };
            Unloaded += (s,e) => timer.Stop();
        }
        private bool refreshing;
        private void Refresh() {
            var c = plugin.Controller; if(c == null) return;
            status.Text = plugin.Settings.NativeMode ? NativeGtNeo.Status : c.Status;
            details.Text = c.BaseName + "\nGT Neo firmware " + c.Firmware + (plugin.Settings.NativeMode ? ", direct wheelbase connection" : ", last API request " + c.Latency + " ms");
            error.Text = c.Error + (c.RecoveryPending && !c.Enabled ? "\nA light restore is still pending." : "");
            refreshing = true; enabled.IsChecked = plugin.Settings.Enabled; refreshing = false;
        }
        private static TextBlock Text(string value, double size, FontWeight weight) {
            return new TextBlock { Text = value, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0,12,0,8), Foreground = Brushes.White };
        }
    }
}
