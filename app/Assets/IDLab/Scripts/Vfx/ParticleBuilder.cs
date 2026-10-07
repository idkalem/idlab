using IDLab.App.Rendering;
using UnityEngine;

namespace IDLab.App.Vfx
{
    /// <summary>Code-built particle systems (no assets): steam, smoke, sparks, toxic gas, splashes.</summary>
    public static class ParticleBuilder
    {
        public static ParticleSystem Create(string name, Transform parent, Vector3 localPosition, Color tint, bool additive = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // must be stopped before changing duration
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = additive ? MaterialFactory.Additive(tint) : MaterialFactory.Particle(Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = tint;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            return ps;
        }

        /// <summary>Continuous steam above a container mouth; emission rate is driven by the view.</summary>
        public static ParticleSystem Steam(Transform parent, Vector3 mouth, float radius)
        {
            var ps = Create("Steam", parent, mouth, new Color(1f, 1f, 1f, 0.22f));
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            main.maxParticles = 120;
            main.gravityModifier = -0.01f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // cone points along +Z; rotate it to +Y
            shape.angle = 8f;
            shape.radius = radius * 0.8f;
            shape.rotation = new Vector3(-90, 0, 0);

            FadeAndGrow(ps, 2.5f);
            ps.Play();
            return ps;
        }

        /// <summary>One-shot burst that destroys itself when finished.</summary>
        public static ParticleSystem Burst(string name, Vector3 worldPosition, Color tint, int count, float speed, float size, float lifetime,
            float gravity, bool additive, float growth = 2f, float duration = 0.1f, float rate = 0f)
        {
            var ps = Create(name, null, worldPosition, tint, additive);
            var main = ps.main;
            main.loop = false;
            main.duration = Mathf.Max(0.05f, duration);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.gravityModifier = gravity;
            main.maxParticles = Mathf.Max(count, 10) + Mathf.CeilToInt(rate * duration);
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = rate;
            if (count > 0)
            {
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            }

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            FadeAndGrow(ps, growth);
            ps.Play();
            return ps;
        }

        private static void FadeAndGrow(ParticleSystem ps, float growth)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, growth));
        }
    }
}
