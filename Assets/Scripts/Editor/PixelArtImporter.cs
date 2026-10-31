using UnityEditor;
using UnityEngine;

/// <summary>
/// Кожна НОВА картинка, яку кладуть у Assets/Sprites, автоматично отримує налаштування піксель-арту:
/// Sprite, Point (no filter), без стиснення, 32 пікселі на одиницю.
/// Якщо потім змінити налаштування вручну, цей скрипт їх не чіпатиме.
/// </summary>
public class PixelArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Sprites/")) return;
        if (!assetImporter.importSettingsMissing) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 32f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
    }
}
