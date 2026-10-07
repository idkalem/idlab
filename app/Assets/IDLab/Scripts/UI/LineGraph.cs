using IDLab.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace IDLab.App.UI
{
    /// <summary>
    /// Allocation-light live line chart drawn directly into the canvas mesh (no textures, no plugin).
    /// Rebuilds only when the series version, axis window or rect size changes; decimates to ~1 point per pixel.
    /// </summary>
    public sealed class LineGraph : MaskableGraphic
    {
        public DataSeries Series;
        public double XMin, XMax = 1, YMin, YMax = 1;
        public Color LineColor = Color.white;
        public Color GridColor = UITheme.GraphGrid;
        public float Thickness = 2.2f;
        public int HorizontalGridLines = 4;
        public int VerticalGridLines = 6;

        private int _lastVersion = -1;
        private Vector4 _lastWindow;
        private Vector2 _lastSize;

        public void SetWindow(double xMin, double xMax, double yMin, double yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
        }

        private void Update()
        {
            var window = new Vector4((float)XMin, (float)XMax, (float)YMin, (float)YMax);
            int version = Series?.Version ?? -1;
            Vector2 size = rectTransform.rect.size;
            if (version != _lastVersion || window != _lastWindow || size != _lastSize)
            {
                _lastVersion = version;
                _lastWindow = window;
                _lastSize = size;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width < 4 || r.height < 4)
            {
                return;
            }

            for (int i = 0; i <= HorizontalGridLines; i++)
            {
                float y = r.yMin + r.height * i / HorizontalGridLines;
                Segment(vh, new Vector2(r.xMin, y), new Vector2(r.xMax, y), 1f, GridColor);
            }

            for (int i = 0; i <= VerticalGridLines; i++)
            {
                float x = r.xMin + r.width * i / VerticalGridLines;
                Segment(vh, new Vector2(x, r.yMin), new Vector2(x, r.yMax), 1f, GridColor);
            }

            if (Series == null || Series.Count < 2 || XMax <= XMin || YMax <= YMin)
            {
                return;
            }

            int first = 0;
            while (first < Series.Count - 1 && Series.TimeAt(first + 1) < XMin)
            {
                first++;
            }

            int visible = Series.Count - first;
            int step = Mathf.Max(1, Mathf.CeilToInt(visible / Mathf.Max(32f, r.width)));
            Vector2 prev = ToLocal(r, Series.TimeAt(first), Series.ValueAt(first));
            for (int i = first + step; i < Series.Count; i += step)
            {
                Vector2 p = ToLocal(r, Series.TimeAt(i), Series.ValueAt(i));
                Segment(vh, prev, p, Thickness, LineColor);
                prev = p;
            }

            Vector2 last = ToLocal(r, Series.LastTime, Series.LastValue);
            Segment(vh, prev, last, Thickness, LineColor);
            Dot(vh, last, Thickness * 2.2f, LineColor);
        }

        private Vector2 ToLocal(Rect r, double x, double y)
        {
            float u = (float)((x - XMin) / (XMax - XMin));
            float v = (float)((y - YMin) / (YMax - YMin));
            return new Vector2(r.xMin + Mathf.Clamp01(u) * r.width, r.yMin + Mathf.Clamp01(v) * r.height);
        }

        private static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector2 n = new Vector2(-d.y, d.x).normalized * (thickness * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero);
            vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        private static void Dot(VertexHelper vh, Vector2 c, float size, Color color)
        {
            float h = size * 0.5f;
            int i = vh.currentVertCount;
            vh.AddVert(c + new Vector2(-h, -h), color, Vector2.zero);
            vh.AddVert(c + new Vector2(-h, h), color, Vector2.zero);
            vh.AddVert(c + new Vector2(h, h), color, Vector2.zero);
            vh.AddVert(c + new Vector2(h, -h), color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
