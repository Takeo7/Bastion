using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bastion.UI.Strategy
{
    /// <summary>
    /// «Otro color» (the 0.10.1's native &lt;input type=color&gt;, which UI Toolkit has none of at runtime): a square of
    /// saturation and value for the hue picked on the strip beside it. While dragging it previews (at most every 90 ms,
    /// as screen.ts's drag()); letting go commits.
    /// </summary>
    public sealed class ColorPicker : VisualElement
    {
        const int Size = 64;
        /// <summary>The preview's pace while dragging (screen.ts: 90 ms).</summary>
        const float PreviewSeconds = 0.09f;

        static Texture2D hueStrip;
        readonly Texture2D square = new Texture2D(Size, Size, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        readonly VisualElement field;
        readonly VisualElement strip;
        readonly VisualElement fieldKnob;
        readonly VisualElement stripKnob;
        float h, s, v;
        float lastPreview;

        /// <summary>The colour now; `preview` while dragging, false once let go.</summary>
        public event Action<Color, bool> Changed;

        public ColorPicker(Color start)
        {
            AddToClassList("color-picker");
            Color.RGBToHSV(start, out h, out s, out v);
            field = new VisualElement();
            field.AddToClassList("color-picker__field");
            field.style.backgroundImage = new StyleBackground(square);
            fieldKnob = new VisualElement { pickingMode = PickingMode.Ignore };
            fieldKnob.AddToClassList("color-picker__knob");
            field.Add(fieldKnob);
            strip = new VisualElement();
            strip.AddToClassList("color-picker__strip");
            strip.style.backgroundImage = new StyleBackground(HueStrip());
            stripKnob = new VisualElement { pickingMode = PickingMode.Ignore };
            stripKnob.AddToClassList("color-picker__bar");
            strip.Add(stripKnob);
            Add(field);
            Add(strip);
            Drag(field, (x, y) => { s = x; v = 1 - y; });
            Drag(strip, (x, y) => { h = y; Paint(); });
            Paint();
            RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        /// <summary>A drag inside `target`: its place (0–1 each way) to `set`, previewing as it goes and committing on release.</summary>
        void Drag(VisualElement target, Action<float, float> set)
        {
            void At(Vector2 p, bool preview)
            {
                var r = target.contentRect;
                set(Mathf.Clamp01(p.x / Mathf.Max(1, r.width)), Mathf.Clamp01(p.y / Mathf.Max(1, r.height)));
                Place();
                if (preview && Time.unscaledTime - lastPreview < PreviewSeconds) return;
                lastPreview = Time.unscaledTime;
                Changed?.Invoke(Color.HSVToRGB(h, s, v), preview);
            }
            target.RegisterCallback<PointerDownEvent>(e =>
            {
                target.CapturePointer(e.pointerId);
                At(e.localPosition, true);
            });
            target.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (target.HasPointerCapture(e.pointerId)) At(e.localPosition, true);
            });
            target.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!target.HasPointerCapture(e.pointerId)) return;
                target.ReleasePointer(e.pointerId);
                At(e.localPosition, false);
            });
        }

        /// <summary>The square for the hue now.</summary>
        void Paint()
        {
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    pixels[y * Size + x] = Color.HSVToRGB(h, x / (Size - 1f), y / (Size - 1f));
            square.SetPixels32(pixels);
            square.Apply();
            field.MarkDirtyRepaint();
        }

        void Place()
        {
            var r = field.contentRect;
            fieldKnob.style.left = s * r.width - 5;
            fieldKnob.style.top = (1 - v) * r.height - 5;
            stripKnob.style.top = h * strip.contentRect.height - 1;
        }

        static Texture2D HueStrip()
        {
            if (hueStrip != null) return hueStrip;
            hueStrip = new Texture2D(1, 128, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < 128; y++) hueStrip.SetPixel(0, 127 - y, Color.HSVToRGB(y / 127f, 1, 1));
            hueStrip.Apply();
            return hueStrip;
        }
    }
}
