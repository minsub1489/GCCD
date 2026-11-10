using UnityEditor;

namespace GCCD.Dts.Editor
{
    // These settings affect only the new, independently licensed scenery resources.
    public sealed class VisualUpgradeAssetImport : AssetPostprocessor
    {
        const string Root = "Assets/Resources/VisualUpgrade/FreeAssets/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.Contains("_nor_gl_");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !assetPath.Contains("_metallic_smoothness_") &&
                                   !assetPath.Contains("_occlusion_") && !assetPath.Contains("_arm_");
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaSource = assetPath.Contains("_metallic_smoothness_")
                ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        }
    }
}
