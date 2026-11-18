using UnityEditor;

/// <summary>
/// Ícones de itens (Assets/Art/Icons, gerados por Tools/Blender/build_item_icons.py):
/// importa como Sprite com transparência, sem mipmaps, para usar em ItemData.icon e na HUD.
/// </summary>
public class IconImportSettings : AssetPostprocessor
{
    const string Folder = "Assets/Art/Icons/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Sprite;
        t.spriteImportMode = SpriteImportMode.Single;
        t.alphaIsTransparency = true;
        t.alphaSource = TextureImporterAlphaSource.FromInput;
        t.mipmapEnabled = false;
        t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        t.maxTextureSize = 256;
        t.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
