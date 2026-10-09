using System;
using System.Drawing;

namespace User.SimagicBridge {
    public sealed class LightFrame {
        public readonly Color[] Rpm;
        public readonly Color[] Buttons;
        public readonly Color[] Encoders;
        public LightFrame(Color[] rpm, Color[] buttons, Color[] encoders) {
            Rpm = Copy(rpm, 15); Buttons = Copy(buttons, 10); Encoders = Copy(encoders, 4);
        }
        private static Color[] Copy(Color[] values, int count) {
            var result = new Color[count];
            for (int i = 0; i < count; i++) result[i] = values != null && i < values.Length ? values[i] : Color.Black;
            return result;
        }
        public static Color Scale(Color color, double brightness) {
            if (Double.IsNaN(brightness) || Double.IsInfinity(brightness)) brightness = 0;
            double factor = Math.Max(0, Math.Min(1, brightness)) * color.A / 255.0;
            return Color.FromArgb(255, (int)Math.Round(color.R * factor), (int)Math.Round(color.G * factor), (int)Math.Round(color.B * factor));
        }
        public static LightFrame FromGroups(Color[] rpm, Color[] buttons, Color[] encoders, Color[] raw,
                                             double rpmBrightness, double buttonsBrightness, double encodersBrightness, Color[] overrides = null) {
            var result = new LightFrame(rpm, buttons, encoders);
            for (int i = 0; i < 15; i++) result.Rpm[i] = Scale(result.Rpm[i], rpmBrightness);
            for (int i = 0; i < 10; i++) result.Buttons[i] = Scale(result.Buttons[i], buttonsBrightness);
            for (int i = 0; i < 4; i++) result.Encoders[i] = Scale(result.Encoders[i], encodersBrightness);
            ApplyOverlay(result, raw);
            ApplyOverlay(result, overrides);
            return result;
        }
        private static void ApplyOverlay(LightFrame result, Color[] colors) {
            if (colors != null) for (int i = 0; i < Math.Min(29, colors.Length); i++) {
                if (colors[i].A == 0) continue;
                var color = Scale(colors[i], 1);
                if (i < 15) result.Rpm[i] = color;
                else if (i < 25) result.Buttons[i - 15] = color;
                else result.Encoders[i - 25] = color;
            }
        }
        public static string Hex(Color color) { return String.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B); }
    }
}
