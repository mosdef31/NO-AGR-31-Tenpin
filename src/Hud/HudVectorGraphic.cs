using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RocketPod.Hud
{

    internal sealed class HudVectorGraphic : MaskableGraphic
    {
        private struct Stroke
        {
            public Vector2[] Points;
            public float Width;
            public Color Color;
            public bool Closed;
        }

        private readonly List<Stroke> _strokes = new List<Stroke>();

        internal void Begin() => _strokes.Clear();

        internal void End() => SetVerticesDirty();

        internal void Line(Vector2 a, Vector2 b, float width, Color color)
        {
            _strokes.Add(new Stroke
            {
                Points = new[] { a, b },
                Width = width,
                Color = color,
                Closed = false,
            });
        }

        internal void Polyline(Vector2[] points, float width, Color color, bool closed = false)
        {
            if (points == null || points.Length < 2) return;
            _strokes.Add(new Stroke
            {
                Points = points,
                Width = width,
                Color = color,
                Closed = closed,
            });
        }

        internal void Circle(Vector2 centre, float radius, float width, Color color, int segments = 28)
        {
            var pts = new Vector2[segments];
            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                pts[i] = centre + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius;
            }
            Polyline(pts, width, color, closed: true);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            foreach (Stroke s in _strokes)
            {
                int last = s.Closed ? s.Points.Length : s.Points.Length - 1;
                for (int i = 0; i < last; i++)
                {
                    Vector2 p0 = s.Points[i];
                    Vector2 p1 = s.Points[(i + 1) % s.Points.Length];

                    Vector2 dir = p1 - p0;
                    float len = dir.magnitude;
                    if (len < 0.0001f) continue;
                    dir /= len;

                    Vector2 n = new Vector2(-dir.y, dir.x) * (s.Width * 0.5f);

                    int root = vh.currentVertCount;
                    vh.AddVert(p0 - n, s.Color, Vector2.zero);
                    vh.AddVert(p0 + n, s.Color, Vector2.zero);
                    vh.AddVert(p1 + n, s.Color, Vector2.zero);
                    vh.AddVert(p1 - n, s.Color, Vector2.zero);
                    vh.AddTriangle(root, root + 1, root + 2);
                    vh.AddTriangle(root, root + 2, root + 3);
                }
            }
        }
    }
}
