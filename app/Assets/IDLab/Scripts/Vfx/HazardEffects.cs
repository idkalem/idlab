using System.Collections;
using IDLab.Core.Events;
using UnityEngine;

namespace IDLab.App.Vfx
{
    /// <summary>
    /// Turns core <see cref="LabEvent"/>s into feedback: particles, light flashes, camera shake and toasts.
    /// The core decides *that* something dangerous happened; this class only decides how it looks.
    /// </summary>
    public sealed class HazardEffects : MonoBehaviour
    {
        private LabApp _app;

        public void Initialize(LabApp app)
        {
            _app = app;
            app.World.Events.Published += OnEvent;
        }

        private void OnDestroy()
        {
            if (_app != null && _app.World != null)
            {
                _app.World.Events.Published -= OnEvent;
            }
        }

        private void OnEvent(LabEvent e)
        {
            Vector3 p = e.Position.ToUnity();
            float s = Mathf.Clamp01((float)e.Severity);
            switch (e.Type)
            {
                case LabEventType.Explosion:
                    ParticleBuilder.Burst("Fireball", p, new Color(1f, 0.55f, 0.15f, 1f), 70, 1.6f, 0.04f, 0.5f, 0.3f, additive: true, growth: 0.3f);
                    ParticleBuilder.Burst("Sparks", p, new Color(1f, 0.85f, 0.4f, 1f), 50, 2.5f, 0.008f, 0.8f, 1f, additive: true, growth: 0.2f);
                    ParticleBuilder.Burst("Smoke", p, new Color(0.25f, 0.25f, 0.27f, 0.7f), 30, 0.25f, 0.12f, 4f, -0.03f, additive: false, growth: 3f, duration: 1.5f, rate: 25f);
                    StartCoroutine(Flash(p, new Color(1f, 0.6f, 0.25f), 6f * (0.5f + s)));
                    _app.CameraRig.Shake(0.006f + 0.014f * s);
                    break;
                case LabEventType.ToxicGas:
                    ParticleBuilder.Burst("ChlorineGas", p, new Color(0.75f, 0.9f, 0.25f, 0.55f), 20, 0.12f, 0.12f, 6f, -0.015f,
                        additive: false, growth: 3.5f, duration: 3f + 3f * s, rate: 18f);
                    break;
                case LabEventType.Smoke:
                    ParticleBuilder.Burst("Smoke", p, new Color(0.3f, 0.3f, 0.32f, 0.6f), 15, 0.15f, 0.08f, 4f, -0.02f, additive: false, growth: 3f, duration: 2f, rate: 15f);
                    break;
                case LabEventType.Spark:
                case LabEventType.FuseBlown:
                    ParticleBuilder.Burst("Sparks", p, new Color(1f, 0.9f, 0.5f, 1f), 40, 1.8f, 0.006f, 0.5f, 1f, additive: true, growth: 0.2f);
                    StartCoroutine(Flash(p, new Color(0.8f, 0.85f, 1f), 3f));
                    break;
                case LabEventType.Fizz:
                    ParticleBuilder.Burst("Fizz", p, new Color(1f, 1f, 1f, 0.6f), 25, 0.2f, 0.006f, 1f, -0.05f, additive: false, growth: 1f, duration: 1.5f, rate: 20f);
                    break;
                case LabEventType.Spill:
                    ParticleBuilder.Burst("Splash", p, new Color(0.6f, 0.8f, 1f, 0.7f), 18, 0.5f, 0.006f, 0.6f, 1f, additive: false, growth: 0.5f);
                    break;
            }

            if (e.Type != LabEventType.Info)
            {
                _app.Hud?.Toasts?.Show(e);
            }
        }

        private IEnumerator Flash(Vector3 position, Color color, float intensity)
        {
            var light = new GameObject("Flash").AddComponent<Light>();
            light.transform.position = position + Vector3.up * 0.05f;
            light.type = LightType.Point;
            light.color = color;
            light.range = 2.5f;
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                light.intensity = intensity * (1f - t / 0.45f);
                yield return null;
            }

            Destroy(light.gameObject);
        }
    }
}
