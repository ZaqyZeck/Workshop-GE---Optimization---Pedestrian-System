using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TMG.Town.EditorTools
{
    /// <summary>
    /// Entities Graphics can only render a baked SkinnedMeshRenderer correctly when every material on it
    /// uses a shader that supports DOTS skinning (a Shader Graph using the Linear Blend Skinning or
    /// Compute Deformation node). URP/Lit does not, which causes the baking warning:
    /// "Shader [Universal Render Pipeline/Lit] on [BaseHuman] does not support skinning."
    ///
    /// This tool creates a copy of every material used by the SkinnedMeshRenderers in the people prefabs
    /// (including materials embedded in the FBX files), switches the copy to the "ECS/SkinnedLit" Shader Graph,
    /// keeps the original color/smoothness, and assigns the copies back to the prefabs.
    /// The SkinnedLit shader passes vertices through unchanged outside of Entities Graphics, so the prefabs keep
    /// rendering correctly in regular (non-ECS) scenes with an Animator as well.
    /// </summary>
    public static class SkinnedMaterialConverter
    {
        const string k_ShaderPath = "Assets/Shaders/SkinnedLit.shadergraph";
        const string k_OutputFolder = "Assets/Materials/Skinned";
        const string k_PeoplePrefabFolder = "Assets/Prefabs/People";

        [MenuItem("Tools/ECS/Fix Skinned Materials (People Prefabs)")]
        static void ConvertPeoplePrefabs()
        {
            if (!TryGetShader(out var shader))
                return;

            var cache = new Dictionary<Material, Material>();
            int changedRenderers = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { k_PeoplePrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changed = ConvertRenderers(root.GetComponentsInChildren<SkinnedMeshRenderer>(true), shader, cache);
                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedRenderers += changed;
                        Debug.Log($"[SkinnedMaterialConverter] Updated {changed} SkinnedMeshRenderer(s) in {path}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SkinnedMaterialConverter] Done. {changedRenderers} renderer(s) updated, {cache.Count} material(s) processed.");
        }

        [MenuItem("Tools/ECS/Fix Skinned Materials (Selected GameObjects)")]
        static void ConvertSelection()
        {
            if (!TryGetShader(out var shader))
                return;

            var renderers = new List<SkinnedMeshRenderer>();
            foreach (var go in Selection.gameObjects)
                renderers.AddRange(go.GetComponentsInChildren<SkinnedMeshRenderer>(true));

            int changed = ConvertRenderers(renderers, shader, new Dictionary<Material, Material>());
            AssetDatabase.SaveAssets();
            Debug.Log($"[SkinnedMaterialConverter] Updated {changed} SkinnedMeshRenderer(s) in the selection.");
        }

        [MenuItem("Tools/ECS/Fix Skinned Materials (Selected GameObjects)", true)]
        static bool ValidateConvertSelection() => Selection.gameObjects.Length > 0;

        static bool TryGetShader(out Shader shader)
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>(k_ShaderPath);
            if (shader == null)
                Debug.LogError($"[SkinnedMaterialConverter] Could not load skinning shader at '{k_ShaderPath}'.");
            return shader != null;
        }

        static int ConvertRenderers(IEnumerable<SkinnedMeshRenderer> renderers, Shader shader, Dictionary<Material, Material> cache)
        {
            int changedRenderers = 0;
            foreach (var smr in renderers)
            {
                var materials = smr.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var converted = GetOrCreateSkinnedMaterial(materials[i], shader, cache);
                    if (converted != materials[i])
                    {
                        materials[i] = converted;
                        changed = true;
                    }
                }

                if (changed)
                {
                    Undo.RecordObject(smr, "Fix Skinned Materials");
                    smr.sharedMaterials = materials;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
                    EditorUtility.SetDirty(smr);
                    changedRenderers++;
                }
            }
            return changedRenderers;
        }

        static Material GetOrCreateSkinnedMaterial(Material source, Shader shader, Dictionary<Material, Material> cache)
        {
            if (source == null || source.shader == shader)
                return source;

            if (cache.TryGetValue(source, out var cached))
                return cached;

            EnsureFolder(k_OutputFolder);

            // Materials embedded in a model (e.g. Male_Casual.fbx) can share names across models,
            // so prefix them with the model name to keep them unique.
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var baseName = source.name;
            if (!string.IsNullOrEmpty(sourcePath) && !sourcePath.EndsWith(".mat"))
                baseName = $"{Path.GetFileNameWithoutExtension(sourcePath)}_{source.name}";
            foreach (var c in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(c, '_');

            var targetPath = $"{k_OutputFolder}/{baseName}_Skinned.mat";
            var target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (target == null)
            {
                target = new Material(shader) { name = $"{baseName}_Skinned" };

                var color = Color.white;
                if (source.HasProperty("_BaseColor"))
                    color = source.GetColor("_BaseColor");
                else if (source.HasProperty("_Color"))
                    color = source.GetColor("_Color");
                target.SetColor("_BaseColor", color);

                if (source.HasProperty("_Smoothness"))
                    target.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
                else if (source.HasProperty("_Glossiness"))
                    target.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));

                target.enableInstancing = source.enableInstancing;
                AssetDatabase.CreateAsset(target, targetPath);
            }
            else if (target.shader != shader)
            {
                target.shader = shader;
                EditorUtility.SetDirty(target);
            }

            cache[source] = target;
            return target;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
