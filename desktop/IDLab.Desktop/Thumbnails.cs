using System;
using System.Collections.Generic;
using System.Numerics;
using IDLab.Core;
using IDLab.Core.Model;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace IDLab.Desktop
{
    /// <summary>Renders a small 3D picture of every piece of equipment once at start-up (inventory + info pages).</summary>
    internal static class Thumbnails
    {
        private const int Size = 160;

        /// <summary>Light card colour baked behind each picture so clear glassware stays visible on the dark UI.</summary>
        public static readonly Color Background = Gfx.Rgba(0xC6D0DC);
        private static readonly Dictionary<string, RenderTexture2D> Images = new Dictionary<string, RenderTexture2D>();

        public static void Build()
        {
            var world = new LabWorld();
            foreach (var def in world.Catalog.All)
            {
                var inst = world.Spawn(def.Id, Pose3.Identity);
                var item = new Item { Inst = inst };
                var s = item.Size;
                float extent = Math.Max(s.Y, Math.Max(s.X, s.Z));
                var target = new Vector3(0, s.Y * 0.45f, 0);
                float dist = extent * 2.1f + 0.04f;
                var dir = Vector3.Normalize(new Vector3(-0.45f, 0.42f, -1f));
                var cam = new Camera3D(target + dir * dist, target, Vector3.UnitY, 32f, CameraProjection.Perspective);
                var rt = LoadRenderTexture(Size, Size);
                SetTextureFilter(rt.Texture, TextureFilter.Bilinear);
                BeginTextureMode(rt);
                ClearBackground(Background);
                BeginMode3D(cam);
                Gfx.Ambient = new Vector3(0.55f, 0.57f, 0.62f);
                Gfx.BeginFrame(cam.Position);
                Props.DrawOpaque(item, 0);
                Rlgl.DrawRenderBatchActive();
                Rlgl.DisableDepthMask();
                Props.DrawTransparent(item);
                Rlgl.DrawRenderBatchActive();
                Rlgl.EnableDepthMask();
                EndMode3D();
                EndTextureMode();
                Images[def.Id] = rt;
                Bubbles.Forget(inst);
            }
        }

        /// <summary>Draws the thumbnail of <paramref name="definitionId"/> into a screen rectangle.</summary>
        public static void Draw(string definitionId, Rectangle dest, float alpha = 1f)
        {
            if (!Images.TryGetValue(definitionId, out var rt)) return;
            DrawRectangleRounded(dest, 0.2f, 6, Gfx.WithAlpha(Background, alpha));
            float inset = Math.Max(2f, dest.Width * 0.05f);
            dest = new Rectangle(dest.X + inset, dest.Y + inset, dest.Width - 2 * inset, dest.Height - 2 * inset);
            DrawTexturePro(rt.Texture, new Rectangle(0, 0, Size, -Size), dest, Vector2.Zero, 0, Gfx.WithAlpha(Color.White, alpha));
        }
    }
}
