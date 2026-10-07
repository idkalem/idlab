using UnityEngine;

namespace IDLab.App
{
    /// <summary>
    /// Zero-setup entry point: pressing Play in any scene (even an empty one) builds the whole lab in code,
    /// so the repository needs no binary scene/prefab assets. Add a GameObject named "IDLab_NoAutoBoot" to a
    /// scene to opt out (e.g. a future main-menu scene).
    /// </summary>
    public static class LabBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<LabApp>() != null || GameObject.Find("IDLab_NoAutoBoot") != null)
            {
                return;
            }

            new GameObject("ID Lab").AddComponent<LabApp>();
        }
    }
}
