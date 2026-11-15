using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    /// <summary>
    /// Ustawienia importu dla zasobów z Art/ThirdParty (bez zmiany samych plików źródłowych).
    /// </summary>
    public class ThirdPartyImportRules : AssetPostprocessor
    {
        const string Root = "Assets/_Mleczarka/Art/";

        static bool Is(string path, string part) => path.Replace('\\', '/').Contains(part);

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var mi = (ModelImporter)assetImporter;
            mi.isReadable = Is(assetPath, "/Bodies/"); // ciało czytelne: wycinamy z niego samą głowę
            mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = Is(assetPath, "/UniversalBaseCharacters/");

            bool humanBody = Is(assetPath, "/UniversalBaseCharacters/Bodies/");
            bool anims = Is(assetPath, "/UniversalAnimationLibrary");
            bool skinnedPart = Is(assetPath, "/UniversalBaseCharacters/Hair/") || Is(assetPath, "/ModularOutfitsFantasy/");

            if (humanBody || anims)
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = anims;
            }
            else if (Is(assetPath, "/FarmAnimals/"))
            {
                // zwierzęta (krowa): szkielet Generic z własnymi animacjami (Idle, WalkSlow…)
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.importAnimation = true;
            }
            else if (skinnedPart)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.importAnimation = false;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (Is(assetPath, "/HDRI/"))
            {
                // niebo HDRI: bez mipmap (bez szwów), poziomo zawijane
                ti.textureShape = TextureImporterShape.Texture2D;
                ti.mipmapEnabled = false;
                ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var hs = ti.GetPlatformTextureSettings(platform);
                    hs.overridden = true; hs.maxTextureSize = 1024; hs.format = TextureImporterFormat.ASTC_HDR_6x6;
                    ti.SetPlatformTextureSettings(hs);
                }
                return;
            }
            bool normal = n.EndsWith("_Normal");
            bool linear = n.Contains("Roughness") || n.Contains("_ORM") || n.EndsWith("_MS") || n.EndsWith("_AO") || n.Contains("Noise");
            ti.textureShape = TextureImporterShape.Texture2D;
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            else if (linear) ti.sRGBTexture = false;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = n.Contains("Naklejka") || n.Contains("Leaf") || n.Contains("Leaves") || n.Contains("Grass") || n.Contains("Flowers") || n.Contains("Vine");
            ti.maxTextureSize = 2048;

            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var s = ti.GetPlatformTextureSettings(platform);
                s.overridden = true;
                s.maxTextureSize = 1024;
                s.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(s);
            }
        }
    }
}
