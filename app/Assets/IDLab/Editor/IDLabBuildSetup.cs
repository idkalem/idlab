using IDLab.App.Rendering;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace IDLab.EditorTools
{
    /// <summary>
    /// The prototype creates every material at runtime via <see cref="MaterialFactory"/>. Player builds strip
    /// shaders/variants nothing references, so before each build we save one material per variant the factory
    /// uses into a Resources folder (Resources content is always included, with its keyword variants).
    /// </summary>
    public sealed class IDLabBuildSetup : IPreprocessBuildWithReport
    {
        private const string Folder = "Assets/IDLab/Resources/IDLabShaderRefs";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => GenerateShaderReferences();

        [MenuItem("ID Lab/Generate Shader References")]
        public static void GenerateShaderReferences()
        {
            if (!AssetDatabase.IsValidFolder("Assets/IDLab/Resources"))
            {
                AssetDatabase.CreateFolder("Assets/IDLab", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/IDLab/Resources", "IDLabShaderRefs");
            }

            Save("Opaque", MaterialFactory.Opaque(Color.white));
            Save("Transparent", MaterialFactory.Transparent(Color.white));
            Save("Emissive", MaterialFactory.Emissive(Color.white, Color.white));
            Save("Additive", MaterialFactory.Additive(Color.white));
            Save("Particle", MaterialFactory.Particle(Color.white));
            Save("Line", MaterialFactory.Line());
            AssetDatabase.SaveAssets();
            Debug.Log("[IDLab] Shader reference materials generated in " + Folder);
        }

        private static void Save(string name, Material source)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var copy = new Material(source) { name = name };
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
            }
            else
            {
                AssetDatabase.CreateAsset(copy, path);
            }
        }
    }
}
