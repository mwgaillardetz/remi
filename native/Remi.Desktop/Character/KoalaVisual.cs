using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Remi.Core;

namespace Remi.Desktop.Character;

public enum Mood { Idle, Thinking, Listening, Talking, Excited, Singing }

/// <summary>Retained vector artwork; no render loop, texture uploads, or 3D scene.</summary>
public sealed class KoalaVisual : FrameworkElement
{
    private static readonly Dictionary<string, Geometry> Geometries = new();
    private static readonly Dictionary<string, Brush> BrushesByColor = new();
    private readonly DispatcherTimer blinkTimer;
    private readonly DispatcherTimer eyesTimer;
    private readonly RotateTransform tilt = new(0, 150, 240);
    private readonly TranslateTransform bounce = new();
    private Settings settings = new();
    private Mood mood;
    private bool eyesClosed;
    private bool mouthOpen;

    public KoalaVisual()
    {
        var transforms = new TransformGroup();
        transforms.Children.Add(tilt);
        transforms.Children.Add(bounce);
        RenderTransform = transforms;
        blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6.4) };
        eyesTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        blinkTimer.Tick += (_, _) => { eyesClosed = true; InvalidateVisual(); eyesTimer.Start(); };
        eyesTimer.Tick += (_, _) => { eyesTimer.Stop(); eyesClosed = false; InvalidateVisual(); };
        IsVisibleChanged += (_, _) => UpdateMotion();
        Unloaded += (_, _) => { blinkTimer.Stop(); eyesTimer.Stop(); StopAnimation(); };
        SnapsToDevicePixels = true;
    }

    public void Configure(Settings value) { settings = value; UpdateMotion(); InvalidateVisual(); }
    public void SetMood(Mood value) { mood = value; mouthOpen = value is Mood.Talking or Mood.Singing; UpdateMotion(); InvalidateVisual(); }
    public void Articulate(bool open) { if (!settings.ReducedMotion) { mouthOpen = open; InvalidateVisual(); } }

    private void StopAnimation()
    {
        tilt.BeginAnimation(RotateTransform.AngleProperty, null);
        bounce.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private void UpdateMotion()
    {
        StopAnimation();
        blinkTimer.Stop(); eyesTimer.Stop(); eyesClosed = false;
        if (!IsVisible || settings.ReducedMotion) return;
        blinkTimer.Start();
        if (mood == Mood.Idle) return;
        var angle = settings.CharacterStyle == "cute" ? 2 : .7;
        var animation = new DoubleAnimation(-angle, angle, TimeSpan.FromMilliseconds(mood == Mood.Singing ? 420 : 900))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
        Timeline.SetDesiredFrameRate(animation, 20);
        tilt.BeginAnimation(RotateTransform.AngleProperty, animation);
        if (mood is Mood.Excited or Mood.Singing)
        {
            var hop = new DoubleAnimation(0, settings.CharacterStyle == "cute" ? -5 : -2, TimeSpan.FromMilliseconds(420)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(hop, 20);
            bounce.BeginAnimation(TranslateTransform.YProperty, hop);
        }
    }

    private static Brush Color(string color)
    {
        if (!BrushesByColor.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(color));
            brush.Freeze(); BrushesByColor[color] = brush;
        }
        return brush;
    }

    private static Brush Gradient(string light, string dark)
    {
        var key = light + dark;
        if (!BrushesByColor.TryGetValue(key, out var brush))
        {
            brush = new LinearGradientBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(light),
                (System.Windows.Media.Color)ColorConverter.ConvertFromString(dark), new Point(.2, 0), new Point(.8, 1));
            brush.Freeze(); BrushesByColor[key] = brush;
        }
        return brush;
    }

    private static Geometry Shape(string data)
    {
        if (!Geometries.TryGetValue(data, out var geometry)) { geometry = Geometry.Parse(data); geometry.Freeze(); Geometries[data] = geometry; }
        return geometry;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.PushTransform(new ScaleTransform(ActualWidth / 300, ActualHeight / 320));
        if (settings.CharacterStyle == "professional") { DrawProfessional(dc); dc.Pop(); return; }
        if (settings.CharacterStyle == "robot") { DrawRobot(dc); dc.Pop(); return; }
        var (light, fur, dark) = settings.Fur switch
        {
            "amber" => ("#E7BC81", "#C58B4F", "#925C35"),
            "black" => ("#777E81", "#535E62", "#37464C"),
            _ => ("#C9D5D2", "#9FAFAB", "#748D86")
        };
        var coat = Gradient(light, fur);
        var edge = new Pen(Color(dark), 1.5);
        void Ellipse(Brush b, double x, double y, double rx, double ry, Pen? p = null) => dc.DrawEllipse(b, p, new Point(x, y), rx, ry);
        void Path(string data, Brush? b, Pen? p = null) => dc.DrawGeometry(b, p, Shape(data));
        // Rounded silhouette, a warm muzzle, and asymmetry keep the little companion approachable.
        Ellipse(Color("#200E3027"), 150, 303, 76, 8);
        Path("M94,197 C69,211 66,264 93,283 L207,283 C234,263 231,211 206,197 Z", coat, edge);
        Ellipse(Gradient("#F5F3E9", "#E1E5D9"), 150, 242, 45, 47);
        Ellipse(coat, 99, 286, 31, 18, edge); Ellipse(coat, 201, 286, 31, 18, edge);
        Ellipse(Color("#82948B"), 98, 290, 16, 6); Ellipse(Color("#82948B"), 202, 290, 16, 6);
        // Large scalloped ears with inset velvet.
        Path("M85,70 C61,38 28,43 26,69 Q16,74 23,84 Q12,91 23,101 Q15,112 29,117 Q25,127 38,130 C44,151 73,152 94,126 Z", coat, edge);
        Path("M215,70 C239,38 272,43 274,69 Q284,74 277,84 Q288,91 277,101 Q285,112 271,117 Q275,127 262,130 C256,151 227,152 206,126 Z", coat, edge);
        Ellipse(Gradient("#E4D3C8", "#B5A89F"), 57, 98, 26, 33);
        Ellipse(Gradient("#E4D3C8", "#B5A89F"), 243, 98, 26, 33);
        Path("M43,79 Q34,96 44,112 M257,79 Q266,96 256,112", null, new Pen(Color("#ECE4D9"), 3));
        // Softly pear-shaped head and the signature little crown tuft.
        Path("M68,125 C63,89 87,63 119,62 Q128,48 138,61 Q147,47 157,60 Q170,50 181,65 C218,68 237,97 232,131 C240,164 222,194 194,205 C168,218 132,218 106,205 C77,194 59,164 68,125 Z", coat, edge);
        Path("M84,116 C89,85 109,77 127,76 M174,76 C193,78 211,91 216,111", null, new Pen(Color("#E3EAE2"), 3));
        Ellipse(Gradient("#F5F3E9", "#DFE3D9"), 150, 168, 65, 39);
        Ellipse(Color("#35C58D83"), 98, 158, 14, 8); Ellipse(Color("#35C58D83"), 202, 158, 14, 8);
        // Brows, glossy eyes, and tiny highlights.
        Path(mood == Mood.Thinking ? "M100,115 Q109,109 119,115 M181,112 Q191,107 201,114" : "M100,114 Q109,109 119,113 M181,113 Q191,109 201,114", null, new Pen(Color(dark), 2.5));
        if (eyesClosed)
            Path("M101,135 Q110,142 120,134 M180,134 Q190,142 199,135", null, new Pen(Color("#283B37"), 3));
        else
        {
            Ellipse(Color("#283B37"), 111, 134, 8, 10); Ellipse(Color("#283B37"), 189, 134, 8, 10);
            Ellipse(Color("#FFFFFF"), 108, 130, 2.8, 3); Ellipse(Color("#FFFFFF"), 186, 130, 2.8, 3);
            Ellipse(Color("#7C998D"), 114, 138, 1.5, 1.5); Ellipse(Color("#7C998D"), 192, 138, 1.5, 1.5);
        }
        Path("M132,139 C132,126 168,126 168,139 L166,160 Q150,178 134,160 Z", Gradient("#465651", "#253B35"));
        Ellipse(Color("#71817A"), 145, 137, 7, 3);
        Path("M150,168 L150,176 M137,177 Q150,188 163,177", null, new Pen(Color("#53665D"), 2));
        if (mouthOpen) { Ellipse(Color("#4F4944"), 150, 181, 8, 6); Ellipse(Color("#D4A89A"), 150, 184, 4.5, 2); }
        // Eucalyptus neckerchief: a recognizable accent that also ties into the app palette.
        Path("M102,200 Q150,222 199,200 L191,216 Q152,235 109,217 Z", Gradient("#6C9179", "#426B58"));
        Path("M155,221 L176,244 184,218 Z", Color("#426B58"));
        Path("M163,224 L174,235", null, new Pen(Color("#A5B99B"), 1.5));
        if (settings.Outfit != "none")
        {
            var shirt = settings.Outfit == "tuxedo" ? Gradient("#45545A", "#253A40") : Gradient("#85A494", "#628271");
            Path("M105,217 Q150,237 195,217 L209,270 Q151,287 91,270 Z", shirt);
            if (settings.Outfit == "tuxedo")
            {
                Path("M130,223 L150,263 170,223 Z", Color("#F8F5EC"));
                Path("M136,229 L150,234 164,229 164,240 150,235 136,240 Z", Color("#B58F72"));
            }
            else if (settings.Outfit == "hawaiian")
            {
                foreach (var p in new[] { new Point(119, 240), new Point(178, 249), new Point(137, 269), new Point(194, 268) })
                {
                    Ellipse(Color("#E9D8BA"), p.X - 4, p.Y, 4, 2.5); Ellipse(Color("#E9D8BA"), p.X + 4, p.Y, 4, 2.5);
                    Ellipse(Color("#E9D8BA"), p.X, p.Y - 4, 2.5, 4); Ellipse(Color("#E9D8BA"), p.X, p.Y + 4, 2.5, 4);
                    Ellipse(Color("#AC7D57"), p.X, p.Y, 2, 2);
                }
            }
        }
        // Paws overlap the body instead of looking attached at the shoulders.
        Path("M93,210 C72,213 73,251 91,260 C104,266 113,254 107,241 L102,219 Z", coat, edge);
        Path("M207,210 C228,213 227,251 209,260 C196,266 187,254 193,241 L198,219 Z", coat, edge);
        Path("M87,248 L91,253 M93,245 L98,252 M213,248 L209,253 M207,245 L202,252", null, new Pen(Color(dark), 1.4));
        // A small botanical detail held in the right paw.
        Path("M207,250 Q223,224 233,198", null, new Pen(Color("#617F61"), 2));
        Path("M221,223 C203,223 206,209 218,213 Q224,216 221,223 M225,215 C244,216 246,201 234,205 Q226,207 225,215 M230,204 C219,197 224,185 233,191 Q237,196 230,204", Gradient("#ADC09B", "#6E946F"));
        if (settings.Sunglasses)
        {
            dc.DrawRoundedRectangle(Color("#283C3B"), new Pen(Color("#B99B69"), 2), new Rect(94, 124, 36, 24), 8, 8);
            dc.DrawRoundedRectangle(Color("#283C3B"), new Pen(Color("#B99B69"), 2), new Rect(170, 124, 36, 24), 8, 8);
            Path("M130,131 Q150,121 170,131 M103,130 L118,130 M179,130 L194,130", null, new Pen(Color("#B99B69"), 2));
        }
        dc.Pop();
    }

    private void DrawProfessional(DrawingContext dc)
    {
        var (light, fur, dark) = settings.Fur switch
        {
            "amber" => ("#E8C18B", "#C59359", "#8F633D"),
            "black" => ("#89949C", "#596974", "#344955"),
            _ => ("#D4DEDF", "#A0B3B8", "#708B94")
        };
        var coat = Gradient(light, fur); var edge = new Pen(Color(dark), 1.2);
        void E(Brush b, double x, double y, double rx, double ry, Pen? p = null) => dc.DrawEllipse(b, p, new Point(x, y), rx, ry);
        void P(string data, Brush? b, Pen? p = null) => dc.DrawGeometry(b, p, Shape(data));
        E(Color("#180E2030"), 150, 303, 62, 7);
        // Longer silhouette, tailored shoulders, and quiet details give the signature look its poise.
        P("M110,242 L145,243 142,286 Q130,298 106,288 Z M155,243 L190,242 194,288 Q170,298 158,286 Z", Gradient("#425762", "#273B48"));
        dc.DrawRoundedRectangle(Gradient("#3D515D", "#233743"), null, new Rect(102, 283, 42, 17), 8, 8);
        dc.DrawRoundedRectangle(Gradient("#3D515D", "#233743"), null, new Rect(156, 283, 42, 17), 8, 8);
        P("M102,174 Q84,181 79,207 L75,249 Q83,263 96,253 L111,206 Z M198,174 Q216,181 221,207 L225,249 Q217,263 204,253 L189,206 Z", Gradient("#405C6D", "#263E4D"));
        E(coat, 85, 254, 12, 15, edge); E(coat, 215, 254, 12, 15, edge);
        var jacket = settings.Outfit switch { "plain" => Gradient("#778F87", "#4F6C63"), "hawaiian" => Gradient("#447B7B", "#285B63"), "tuxedo" => Gradient("#34404B", "#182A36"), _ => Gradient("#456272", "#293F50") };
        P("M111,167 Q150,158 189,167 Q207,186 204,225 L196,267 Q151,279 104,267 L96,225 Q93,186 111,167 Z", jacket);
        P("M126,169 Q150,177 174,169 L165,246 135,246 Z", Gradient("#F3EEE4", "#D7DDD8"));
        P("M114,169 L129,167 148,225 121,209 127,197 111,192 Z M186,169 L171,167 152,225 179,209 173,197 189,192 Z", Gradient("#607F8F", "#354F62"));
        P("M150,226 L150,266 M172,243 L191,241", null, new Pen(Color("#8BA0A7"), 1));
        E(Color("#BFC9C6"), 154, 240, 1.7, 1.7); E(Color("#BFC9C6"), 154, 257, 1.7, 1.7);
        if (settings.Outfit == "plain") P("M116,177 Q150,197 184,177 L183,252 Q150,264 117,252 Z", Gradient("#829C8E", "#577969"));
        if (settings.Outfit == "tuxedo") P("M137,181 L150,186 163,181 163,192 150,187 137,192 Z", Color("#B89D74"));
        if (settings.Outfit == "hawaiian")
            foreach (var p in new[] { new Point(116, 222), new Point(183, 233), new Point(129, 256) })
            { E(Color("#DDC9A4"), p.X - 3, p.Y, 4, 2); E(Color("#DDC9A4"), p.X + 3, p.Y, 4, 2); E(Color("#DDC9A4"), p.X, p.Y, 2, 5); }
        // Brushed brass lapel pin.
        dc.DrawRoundedRectangle(Color("#C6AF80"), null, new Rect(178, 201, 4, 10), 2, 2);
        E(coat, 77, 92, 32, 36, edge); E(coat, 223, 92, 32, 36, edge);
        E(Gradient("#E1DCD8", "#ADADB0"), 75, 94, 22, 26); E(Gradient("#E1DCD8", "#ADADB0"), 225, 94, 22, 26);
        P("M82,108 C78,70 101,47 139,46 Q150,35 160,47 C201,48 223,72 218,110 L215,133 Q208,171 177,183 Q150,193 123,183 Q91,171 85,133 Z", coat, edge);
        P("M98,85 Q108,63 135,61 M167,61 Q193,63 204,84", null, new Pen(Color("#E7EDED"), 2));
        E(Gradient("#EAEDE7", "#D6DED9"), 150, 145, 47, 29);
        P(mood == Mood.Thinking ? "M109,106 Q118,100 127,104 M173,103 Q183,97 193,102" : "M108,103 Q118,99 127,103 M173,103 Q183,99 192,103", null, new Pen(Color(dark), 2));
        if (eyesClosed) P("M109,118 Q118,123 127,118 M173,118 Q182,123 191,118", null, new Pen(Color("#253D48"), 2.6));
        else
        {
            E(Color("#253D48"), 119, 117, 6, 7); E(Color("#253D48"), 181, 117, 6, 7);
            E(Color("#F6FAFA"), 117, 114, 1.8, 2); E(Color("#F6FAFA"), 179, 114, 1.8, 2);
        }
        P("M135,128 C134,116 166,116 165,128 L163,146 Q150,158 137,146 Z", Gradient("#50616A", "#293E48"));
        E(Color("#85949A"), 145, 126, 5, 2);
        P("M150,153 L150,158 M139,159 Q150,167 161,159", null, new Pen(Color("#5E7479"), 1.7));
        if (mouthOpen) E(Color("#40575F"), 150, 163, 5, 3.5);
        if (settings.Sunglasses)
        {
            dc.DrawRoundedRectangle(Color("#233B46"), new Pen(Color("#B9A378"), 1.5), new Rect(103, 109, 31, 19), 6, 6);
            dc.DrawRoundedRectangle(Color("#233B46"), new Pen(Color("#B9A378"), 1.5), new Rect(166, 109, 31, 19), 6, 6);
            P("M134,115 Q150,110 166,115", null, new Pen(Color("#B9A378"), 1.5));
        }
    }

    private void DrawRobot(DrawingContext dc)
    {
        void E(Brush b, double x, double y, double rx, double ry, Pen? p = null) => dc.DrawEllipse(b, p, new Point(x, y), rx, ry);
        void P(string data, Brush? b, Pen? p = null) => dc.DrawGeometry(b, p, Shape(data));
        var shell = Gradient("#FAFCFC", "#BDCCD5"); var red = Gradient("#E96558", "#A62F35");
        var gold = Gradient("#F5D68D", "#B88B44"); var edge = new Pen(Color("#8197A6"), 1.2);
        E(Color("#1C0C203A"), 150, 303, 67, 7);
        // Compact articulated armor, broad boots, and an approachable display face.
        dc.DrawRoundedRectangle(Color("#304553"), null, new Rect(112, 229, 27, 49), 11, 11);
        dc.DrawRoundedRectangle(Color("#304553"), null, new Rect(161, 229, 27, 49), 11, 11);
        P("M111,243 L141,243 139,283 109,283 Z M159,243 L189,243 191,283 161,283 Z", shell, edge);
        dc.DrawRoundedRectangle(gold, null, new Rect(115, 253, 20, 8), 3, 3);
        dc.DrawRoundedRectangle(gold, null, new Rect(165, 253, 20, 8), 3, 3);
        P("M108,277 Q125,271 140,279 L142,297 Q115,306 94,296 L97,286 Z M160,279 Q175,271 192,277 L203,286 206,296 Q185,306 158,297 Z", red, new Pen(Color("#863840"), 1));
        P("M100,293 L136,294 M164,294 L200,293", null, new Pen(Color("#F09B83"), 2));
        E(Color("#334B5B"), 95, 194, 17, 18); E(Color("#334B5B"), 205, 194, 17, 18);
        dc.DrawRoundedRectangle(shell, edge, new Rect(74, 193, 29, 46), 13, 13);
        dc.DrawRoundedRectangle(shell, edge, new Rect(197, 193, 29, 46), 13, 13);
        dc.DrawRoundedRectangle(red, null, new Rect(70, 226, 33, 31), 11, 11);
        dc.DrawRoundedRectangle(red, null, new Rect(197, 226, 33, 31), 11, 11);
        E(gold, 85, 259, 13, 12); E(gold, 215, 259, 13, 12);
        P("M101,165 Q150,151 199,165 L201,213 Q196,242 179,249 L121,249 Q104,242 99,213 Z", red, new Pen(Color("#92353C"), 1.5));
        P("M107,168 L127,172 119,192 100,186 Z M193,168 L173,172 181,192 200,186 Z", gold);
        P("M113,210 L129,222 171,222 187,210 180,239 120,239 Z", Gradient("#A52E39", "#682C3A"));
        P("M120,239 L139,244 161,244 180,239", null, new Pen(Color("#E0B15D"), 3));
        E(Color("#792C3B"), 150, 195, 22, 22); E(gold, 150, 194, 18, 18);
        E(Color("#274657"), 150, 194, 14, 14); E(Color("#5DDCEB"), 150, 194, 10, 10); E(Color("#E9FFFF"), 150, 194, 6, 6);
        // A pearl helmet and red crown frame a deep navy visor; no logos or copied insignia.
        dc.DrawRoundedRectangle(gold, edge, new Rect(59, 91, 21, 42), 9, 9);
        dc.DrawRoundedRectangle(gold, edge, new Rect(220, 91, 21, 42), 9, 9);
        P("M73,101 C72,60 99,36 150,36 C201,36 228,60 227,101 L224,125 Q219,160 193,169 Q150,185 107,169 Q81,160 76,125 Z", shell, edge);
        P("M109,40 Q150,29 191,40 L180,57 Q150,48 120,57 Z", red);
        P("M91,73 Q150,48 209,73 Q221,96 211,128 Q198,154 150,156 Q102,154 89,128 Q79,96 91,73 Z", Gradient("#1C3D58", "#0B1D32"), new Pen(Color("#678698"), 2));
        P("M101,78 Q145,61 189,75", null, new Pen(Color("#3B627A"), 2));
        var eyeHeight = eyesClosed ? 2 : mood == Mood.Excited ? 17 : 13;
        dc.DrawRoundedRectangle(Color("#39A8C9"), null, new Rect(108, 99, 25, eyeHeight + 3), 6, 6);
        dc.DrawRoundedRectangle(Color("#39A8C9"), null, new Rect(167, 99, 25, eyeHeight + 3), 6, 6);
        dc.DrawRoundedRectangle(Color("#B8FAFF"), null, new Rect(111, 99, 19, eyeHeight), 5, 5);
        dc.DrawRoundedRectangle(Color("#B8FAFF"), null, new Rect(170, 99, 19, eyeHeight), 5, 5);
        P(mouthOpen ? "M141,131 Q150,143 159,131 Z" : "M140,132 Q150,140 160,132", mouthOpen ? Color("#83E4F0") : null, new Pen(Color("#83E4F0"), 2));
        P("M91,144 L104,157 M209,144 L196,157", null, new Pen(Color("#D4AB60"), 4));
        E(Color("#A9BFC9"), 150, 165, 3, 2);
    }
}
