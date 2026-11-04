using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deepwave.RetroOS.UI
{
    /// <summary>Which way a <see cref="RetroGradient"/> runs.</summary>
    public enum GradientDirection
    {
        Horizontal,
        Vertical
    }

    /// <summary>Tints a graphic from one colour to another across its rect - the 98 title-bar wash, and what makes the theme's <c>gradientTitleBars</c> switch real. A mesh effect: no material, no draw call, survives the CRT warp.</summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("")]
    public class RetroGradient : BaseMeshEffect
    {
        [Tooltip("Colour at the left (or top) edge.")]
        public Color from = Color.white;

        [Tooltip("Colour at the right (or bottom) edge.")]
        public Color to = Color.white;

        [Tooltip("Which way the wash runs across the rect.")]
        public GradientDirection direction = GradientDirection.Horizontal;

        /// <summary>Adds or updates a gradient on a graphic. Two equal colours leave it flat.</summary>
        public static RetroGradient Apply(Graphic graphic, Color from, Color to,
            GradientDirection direction = GradientDirection.Horizontal)
        {
            if (!graphic) return null;

            var gradient = graphic.GetComponent<RetroGradient>();
            if (!gradient) gradient = graphic.gameObject.AddComponent<RetroGradient>();

            gradient.from = from;
            gradient.to = to;
            gradient.direction = direction;
            gradient.enabled = from != to;
            graphic.SetVerticesDirty();
            return gradient;
        }

        /// <summary>Repaints an existing gradient - a window losing focus.</summary>
        public void Set(Color start, Color end)
        {
            from = start;
            to = end;
            enabled = start != end;
            if (graphic) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0) return;

            var vertices = new List<UIVertex>();
            helper.GetUIVertexStream(vertices);

            var min = float.MaxValue;
            var max = float.MinValue;
            var horizontal = direction == GradientDirection.Horizontal;

            foreach (var vertex in vertices)
            {
                var along = horizontal ? vertex.position.x : vertex.position.y;
                if (along < min) min = along;
                if (along > max) max = along;
            }

            var span = max - min;
            if (span <= Mathf.Epsilon) return;

            for (var i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                var along = horizontal ? vertex.position.x : vertex.position.y;
                var k = (along - min) / span;

                // Vertical runs top to bottom, so `from` is the top colour.
                if (!horizontal) k = 1f - k;

                // Replaces colour, keeps alpha, so a title bar in a fading window still fades.
                var tint = Color.Lerp(from, to, k);
                var alpha = vertex.color.a;
                vertex.color = new Color32(
                    (byte)(tint.r * 255f), (byte)(tint.g * 255f), (byte)(tint.b * 255f), alpha);
                vertices[i] = vertex;
            }

            helper.Clear();
            helper.AddUIVertexTriangleStream(vertices);
        }
    }
}
