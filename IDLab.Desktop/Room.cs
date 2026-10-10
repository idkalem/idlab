using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace IDLab.Desktop
{
    /// <summary>
    /// The laboratory around the student bench: tiled floor and walls, back counter with cabinets and a sink,
    /// a fume hood, glass-door wall cabinets full of reagents, the periodic-table poster, whiteboard, safety
    /// rules, window, ceiling lights, eye wash, safety shower and fire extinguisher. Built once, drawn every frame.
    /// The student bench top is the plane y = 0; the floor is at y = −0.9.
    /// </summary>
    internal static class Room
    {
        private struct Part
        {
            public Mesh Mesh;
            public Matrix4x4 Transform;
            public Color Color;
            public Texture2D? Texture;
            public Vector3 Emission;
            public bool Transparent;
        }

        private const float FloorY = -0.9f, CeilingY = 2.0f, BackZ = 1.5f, SideX = 3.0f, FrontZ = -3.0f;
        private static readonly List<Part> Opaque = new List<Part>();
        private static readonly List<Part> Glass = new List<Part>();

        private static readonly Color Cabinet = new Color(214, 218, 224, 255);
        private static readonly Color CabinetDark = new Color(150, 156, 165, 255);
        private static readonly Color Handle = new Color(120, 126, 134, 255);
        private static readonly Color Paint = new Color(226, 230, 236, 255);

        public static void Build()
        {
            // Floor, ceiling, walls
            Add(Gfx.Quad(9, 9, 9, 9), Gfx.Translate(0, FloorY, -0.75f), Color.White, Textures.Floor);
            Add(Gfx.Quad(9, 9, 1, 1), Gfx.Mul(Gfx.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI)), Gfx.Translate(0, CeilingY, -0.75f)), new Color(236, 238, 241, 255));
            Wall(new Vector3(0, 0, BackZ), new Vector3(0, 0, -1), 2 * SideX);
            Wall(new Vector3(-SideX, 0, -0.75f), new Vector3(1, 0, 0), 9);
            Wall(new Vector3(SideX, 0, -0.75f), new Vector3(-1, 0, 0), 9);
            Wall(new Vector3(0, 0, FrontZ), new Vector3(0, 0, 1), 2 * SideX);

            // Ceiling light panels
            foreach (var (x, z) in new[] { (-1.2f, 0.2f), (1.2f, 0.2f), (-1.2f, -1.6f), (1.2f, -1.6f) })
            {
                Box(new Vector3(x, CeilingY - 0.02f, z), new Vector3(1.2f, 0.03f, 0.6f), new Color(250, 250, 250, 255), new Vector3(1.4f, 1.4f, 1.35f));
            }

            BuildStudentBench();
            BuildBackCounter();
            BuildFumeHood(new Vector3(1.95f, 0, 1.2f));
            BuildWallCabinets();
            BuildWalls();
        }

        // ------------------------------------------------------------------ helpers

        private static void Add(Mesh mesh, Matrix4x4 transform, Color color, Texture2D? texture = null, Vector3 emission = default)
        {
            var p = new Part { Mesh = mesh, Transform = transform, Color = color, Texture = texture, Emission = emission, Transparent = color.A < 255 };
            (p.Transparent ? Glass : Opaque).Add(p);
        }

        private static void Box(Vector3 centre, Vector3 size, Color color, Vector3 emission = default, Texture2D? texture = null) =>
            Add(Gfx.Cube, Gfx.Mul(Gfx.Scale(size.X, size.Y, size.Z), Gfx.Translate(centre)), color, texture, emission);

        private static void Cyl(Vector3 baseCentre, float radius, float height, Color color, Vector3 emission = default) =>
            Add(Gfx.Cylinder, Gfx.Mul(Gfx.Scale(radius, height, radius), Gfx.Translate(baseCentre)), color, null, emission);

        /// <summary>Upright textured rectangle facing <paramref name="facing"/> (image upright, not mirrored).</summary>
        private static void Panel(Vector3 centre, Vector3 facing, float width, float height, Texture2D texture, Color tint, float uRep = 1, float vRep = 1, Vector3 emission = default)
        {
            // Proper rotation: local X → screen-right, local Y (quad normal) → facing, local Z → down (so X × Y = Z).
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, facing));
            var basis = new Matrix4x4(
                right.X, right.Y, right.Z, 0,
                facing.X, facing.Y, facing.Z, 0,
                0, -1, 0, 0,
                0, 0, 0, 1);
            var q = Quaternion.CreateFromRotationMatrix(basis);
            Add(Gfx.Quad(width, height, uRep, vRep, flipV: true), Gfx.Pose(centre, q), tint, texture, emission);
        }

        private static void Wall(Vector3 foot, Vector3 facing, float width)
        {
            const float tileTop = 0.55f;
            float tileH = tileTop - FloorY, paintH = CeilingY - tileTop;
            Panel(new Vector3(foot.X, FloorY + tileH / 2, foot.Z), facing, width, tileH, Textures.WallTiles, Color.White, width / 1f, tileH / 1f);
            Panel(new Vector3(foot.X, tileTop + paintH / 2, foot.Z), facing, width, paintH, Textures.WallTiles, Paint, 1, 1);
            // skirting
            var along = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, facing));
            var size = new Vector3(Math.Abs(along.X) * width + Math.Abs(facing.X) * 0.02f, 0.1f, Math.Abs(along.Z) * width + Math.Abs(facing.Z) * 0.02f);
            Box(new Vector3(foot.X, FloorY + 0.05f, foot.Z) + facing * 0.01f, size, new Color(80, 86, 96, 255));
        }

        // ------------------------------------------------------------------ furniture

        private static void BuildStudentBench()
        {
            // Epoxy top (the work plane y = 0), cabinets with doors, drawers and handles.
            Box(new Vector3(0, -0.02f, 0), new Vector3(1.8f, 0.04f, 0.9f), new Color(28, 30, 35, 255));
            Add(Gfx.Quad(1.8f, 0.9f, 6, 3), Gfx.Translate(0, 0.0003f, 0), Color.White, Textures.Epoxy);
            Box(new Vector3(0, -0.47f, 0.02f), new Vector3(1.72f, 0.86f, 0.82f), Cabinet);
            Box(new Vector3(0, -0.87f, -0.33f), new Vector3(1.72f, 0.06f, 0.06f), new Color(60, 64, 72, 255)); // toe kick
            for (int i = 0; i < 4; i++)
            {
                float x = -0.645f + i * 0.43f;
                Box(new Vector3(x, -0.2f, -0.392f), new Vector3(0.41f, 0.16f, 0.01f), CabinetDark);   // drawer
                Box(new Vector3(x, -0.6f, -0.392f), new Vector3(0.41f, 0.5f, 0.01f), new Color(200, 205, 212, 255)); // door
                Box(new Vector3(x, -0.2f, -0.4f), new Vector3(0.12f, 0.015f, 0.012f), Handle);
                Box(new Vector3(x + 0.15f, -0.45f, -0.4f), new Vector3(0.015f, 0.12f, 0.012f), Handle);
            }

            // Gas taps and sockets along the back edge.
            foreach (float x in new[] { -0.5f, 0.5f })
            {
                Cyl(new Vector3(x, 0, 0.42f), 0.012f, 0.05f, new Color(200, 160, 80, 255));
                Box(new Vector3(x, 0.05f, 0.42f), new Vector3(0.04f, 0.008f, 0.01f), new Color(220, 40, 40, 255));
            }

            // Stools
            foreach (float x in new[] { -1.15f, 1.15f })
            {
                Cyl(new Vector3(x, FloorY, -0.7f), 0.025f, 0.6f, new Color(90, 96, 104, 255));
                Cyl(new Vector3(x, FloorY, -0.7f), 0.18f, 0.02f, new Color(70, 74, 82, 255));
                Cyl(new Vector3(x, FloorY + 0.6f, -0.7f), 0.17f, 0.05f, new Color(40, 90, 150, 255));
            }
        }

        private static void BuildBackCounter()
        {
            float depth = 0.6f, zc = BackZ - depth / 2;
            Box(new Vector3(-0.35f, -0.015f, zc), new Vector3(5.3f, 0.03f, depth), new Color(28, 30, 35, 255));
            Add(Gfx.Quad(5.3f, depth, 16, 2), Gfx.Translate(-0.35f, 0.0003f, zc), Color.White, Textures.Epoxy);
            Box(new Vector3(-0.35f, -0.47f, zc + 0.02f), new Vector3(5.26f, 0.86f, depth - 0.04f), Cabinet);
            for (int i = 0; i < 9; i++)
            {
                float x = -2.85f + 0.3f + i * 0.58f;
                Box(new Vector3(x, -0.48f, zc - depth / 2 + 0.035f), new Vector3(0.56f, 0.76f, 0.01f), new Color(200, 205, 212, 255));
                Box(new Vector3(x + 0.22f, -0.3f, zc - depth / 2 + 0.03f), new Vector3(0.015f, 0.14f, 0.012f), Handle);
            }

            // Sink with gooseneck faucet
            float sx = -1.2f;
            Box(new Vector3(sx, -0.005f, zc - 0.03f), new Vector3(0.6f, 0.012f, 0.42f), new Color(170, 176, 184, 255), default, Textures.Steel);
            Box(new Vector3(sx, -0.004f, zc - 0.03f), new Vector3(0.5f, 0.012f, 0.32f), new Color(110, 116, 124, 255));
            Cyl(new Vector3(sx, 0, BackZ - 0.1f), 0.012f, 0.38f, new Color(190, 196, 204, 255));
            Box(new Vector3(sx, 0.38f, BackZ - 0.18f), new Vector3(0.024f, 0.024f, 0.18f), new Color(190, 196, 204, 255));
            Cyl(new Vector3(sx, 0.3f, BackZ - 0.27f), 0.01f, 0.09f, new Color(190, 196, 204, 255));
            foreach (float dx in new[] { -0.08f, 0.08f })
            {
                Box(new Vector3(sx + dx, 0.08f, BackZ - 0.1f), new Vector3(0.05f, 0.012f, 0.012f), dx < 0 ? new Color(40, 90, 200, 255) : new Color(200, 40, 40, 255));
            }

            // Decorative reagent shelf above the counter (under the poster)
            Box(new Vector3(-0.15f, 0.36f, BackZ - 0.1f), new Vector3(1.5f, 0.02f, 0.18f), new Color(190, 196, 204, 255));
            var colors = new uint[] { 0x8C6A3F, 0x2E7DE0, 0xE8901A, 0x5FD3C8, 0x7A0F8C, 0xFF5A4F, 0x9AD0FF, 0x3CB43C, 0xF5A623 };
            for (int i = 0; i < colors.Length; i++)
            {
                float x = -0.8f + i * 0.16f;
                var vessel = Gfx.Vessel(0.03f, 0.12f, 0.003f, 0.012f, 0.08f);
                Add(vessel, Gfx.Translate(x, 0.37f, BackZ - 0.1f), new Color(200, 225, 255, 70));
                var c = Gfx.Rgba(colors[i]);
                Add(Gfx.Cylinder, Gfx.Mul(Gfx.Scale(0.027f, 0.07f, 0.027f), Gfx.Translate(x, 0.373f, BackZ - 0.1f)), new Color((int)c.R, c.G, c.B, 255));
                Add(Gfx.Cylinder, Gfx.Mul(Gfx.Scale(0.0135f, 0.012f, 0.0135f), Gfx.Translate(x, 0.49f, BackZ - 0.1f)), new Color(40, 40, 46, 255));
            }
        }

        private static void BuildFumeHood(Vector3 c)
        {
            const float w = 1.3f, d = 0.6f, h = 1.6f;
            var white = new Color(232, 235, 240, 255);
            Box(new Vector3(c.X - w / 2, h / 2, c.Z), new Vector3(0.06f, h, d), white);
            Box(new Vector3(c.X + w / 2, h / 2, c.Z), new Vector3(0.06f, h, d), white);
            Box(new Vector3(c.X, h - 0.15f, c.Z), new Vector3(w + 0.06f, 0.3f, d), white);
            Box(new Vector3(c.X, h / 2, c.Z + d / 2 - 0.02f), new Vector3(w, h, 0.03f), new Color(205, 210, 216, 255));
            Box(new Vector3(c.X, h - 0.31f, c.Z - 0.05f), new Vector3(w - 0.1f, 0.02f, 0.06f), new Color(255, 255, 250, 255), new Vector3(1.3f, 1.3f, 1.2f));
            Box(new Vector3(c.X, 0.02f, c.Z), new Vector3(w, 0.04f, d), new Color(60, 64, 70, 255));
            // sash: glass with a frame
            Add(Gfx.Cube, Gfx.Mul(Gfx.Scale(w - 0.04f, 0.8f, 0.01f), Gfx.Translate(c.X, 0.88f, c.Z - d / 2 + 0.02f)), new Color(200, 225, 245, 60));
            Box(new Vector3(c.X, 0.47f, c.Z - d / 2 + 0.02f), new Vector3(w - 0.04f, 0.04f, 0.03f), new Color(70, 76, 86, 255));
            Box(new Vector3(c.X, 1.28f, c.Z - d / 2 + 0.02f), new Vector3(w - 0.04f, 0.03f, 0.03f), new Color(70, 76, 86, 255));
            // a flask and bottles inside
            Add(Gfx.Vessel(0.035f, 0.14f, 0.003f, 0.012f, 0.09f), Gfx.Translate(c.X - 0.3f, 0.04f, c.Z), new Color(200, 225, 255, 70));
            Add(Gfx.Cylinder, Gfx.Mul(Gfx.Scale(0.032f, 0.07f, 0.032f), Gfx.Translate(c.X - 0.3f, 0.045f, c.Z)), new Color(230, 140, 30, 255));
            Add(Gfx.Vessel(0.03f, 0.12f, 0.003f, 0.012f, 0.08f), Gfx.Translate(c.X + 0.2f, 0.04f, c.Z + 0.05f), new Color(200, 225, 255, 70));
        }

        private static void BuildWallCabinets()
        {
            // Glass-door upper cabinets on the left part of the back wall, full of reagent bottles.
            float zc = BackZ - 0.18f;
            for (int i = 0; i < 3; i++)
            {
                float x = -2.55f + i * 0.62f;
                Box(new Vector3(x, 1.1f, zc), new Vector3(0.6f, 0.7f, 0.34f), new Color(225, 229, 234, 255));
                Box(new Vector3(x, 1.1f, zc - 0.168f), new Vector3(0.52f, 0.62f, 0.005f), new Color(120, 130, 140, 255)); // dark back seen through glass
                for (int shelf = 0; shelf < 2; shelf++)
                {
                    float y = 0.82f + shelf * 0.3f;
                    Box(new Vector3(x, y, zc - 0.1f), new Vector3(0.52f, 0.012f, 0.14f), new Color(200, 205, 212, 255));
                    for (int b = 0; b < 4; b++)
                    {
                        var col = Theme.Series[(i * 3 + shelf * 2 + b) % Theme.Series.Length];
                        Add(Gfx.Cylinder, Gfx.Mul(Gfx.Scale(0.03f, 0.12f + (b % 2) * 0.04f, 0.03f), Gfx.Translate(x - 0.18f + b * 0.12f, y + 0.006f, zc - 0.1f)),
                            new Color((int)col.R, col.G, col.B, 255));
                        Add(Gfx.Cylinder, Gfx.Mul(Gfx.Scale(0.014f, 0.02f, 0.014f), Gfx.Translate(x - 0.18f + b * 0.12f, y + 0.13f + (b % 2) * 0.04f, zc - 0.1f)),
                            new Color(40, 40, 46, 255));
                    }
                }

                Add(Gfx.Cube, Gfx.Mul(Gfx.Scale(0.56f, 0.64f, 0.01f), Gfx.Translate(x, 1.1f, zc - 0.175f)), new Color(210, 230, 245, 50));
                Box(new Vector3(x + 0.22f, 0.95f, zc - 0.185f), new Vector3(0.012f, 0.1f, 0.012f), Handle);
            }
        }

        private static void BuildWalls()
        {
            // Back wall: periodic table poster
            Box(new Vector3(-0.15f, 0.94f, BackZ - 0.012f), new Vector3(1.36f, 0.82f, 0.01f), new Color(40, 48, 62, 255));
            Panel(new Vector3(-0.15f, 0.94f, BackZ - 0.02f), new Vector3(0, 0, -1), 1.3f, 0.77f, Textures.Poster, Color.White);

            // Left wall: whiteboard + safety shower + eye wash
            Box(new Vector3(-SideX + 0.015f, 0.95f, 0.1f), new Vector3(0.02f, 0.98f, 1.88f), new Color(170, 176, 184, 255));
            Panel(new Vector3(-SideX + 0.03f, 0.95f, 0.1f), new Vector3(1, 0, 0), 1.8f, 0.9f, Textures.Whiteboard, Color.White);
            Box(new Vector3(-SideX + 0.06f, 0.48f, 0.1f), new Vector3(0.08f, 0.02f, 1.8f), new Color(170, 176, 184, 255));
            Panel(new Vector3(-SideX + 0.03f, 1.05f, 1.05f), new Vector3(1, 0, 0), 0.5f, 0.25f, Textures.EyeWash, Color.White);
            Cyl(new Vector3(-SideX + 0.25f, 1.55f, 1.2f), 0.02f, 0.45f, new Color(30, 150, 80, 255));
            Cyl(new Vector3(-SideX + 0.25f, 1.5f, 1.2f), 0.14f, 0.05f, new Color(30, 150, 80, 255));
            Box(new Vector3(-SideX + 0.25f, 1.1f, 1.2f), new Vector3(0.012f, 0.4f, 0.012f), new Color(30, 150, 80, 255));
            Panel(new Vector3(-SideX + 0.03f, 1.75f, -1.6f), new Vector3(1, 0, 0), 0.5f, 0.25f, Textures.Exit, Color.White, 1, 1, new Vector3(0.2f, 0.4f, 0.25f));

            // Right wall: window, rules, fire extinguisher
            Box(new Vector3(SideX - 0.012f, 1.0f, -0.9f), new Vector3(0.02f, 1.22f, 1.42f), new Color(240, 242, 245, 255));
            Panel(new Vector3(SideX - 0.025f, 1.0f, -0.9f), new Vector3(-1, 0, 0), 1.3f, 1.1f, Textures.Window, Color.White, 1, 1, new Vector3(0.75f, 0.8f, 0.85f));
            Panel(new Vector3(SideX - 0.02f, 0.95f, 0.75f), new Vector3(-1, 0, 0), 0.5f, 0.7f, Textures.Rules, Color.White);
            Cyl(new Vector3(SideX - 0.12f, FloorY + 0.05f, 1.15f), 0.08f, 0.5f, new Color(200, 25, 25, 255));
            Cyl(new Vector3(SideX - 0.12f, FloorY + 0.55f, 1.15f), 0.025f, 0.06f, new Color(30, 30, 30, 255));
            Panel(new Vector3(SideX - 0.02f, 0.05f, 1.15f), new Vector3(-1, 0, 0), 0.36f, 0.18f, Textures.Extinguisher, Color.White);
        }

        // ------------------------------------------------------------------ draw

        public static void DrawOpaque()
        {
            foreach (var p in Opaque)
            {
                if (p.Texture.HasValue) Gfx.DrawTextured(p.Mesh, p.Transform, p.Texture.Value, p.Color, p.Emission, 0.2f);
                else Gfx.Draw(p.Mesh, p.Transform, p.Color, p.Emission);
            }
        }

        public static void DrawTransparent()
        {
            foreach (var p in Glass) Gfx.Draw(p.Mesh, p.Transform, p.Color, p.Emission);
        }
    }
}
