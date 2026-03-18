// KenneyImporter.cs
// Unity Editor script that scans Assets/Art/Kenney/ for Kenney pirate pack PNGs,
// configures their TextureImporter settings for 2D sprite use, and organises them
// into appropriately-named sub-folders inside the AssetDatabase.
//
// How to use:
//   1. Run  scripts/art/download_kenney_packs.py  to fetch the asset packs.
//   2. Open Unity — this script runs automatically via AssetPostprocessor.
//      OR: menu  Booty! > Import Kenney Assets  to trigger manually.
//
// Folder layout produced inside Assets/Art/Kenney/:
//   pirate-pack/Sprites/Ships/
//   pirate-pack/Sprites/Characters/
//   pirate-pack/Sprites/UI/
//   pirate-pack/Sprites/Tiles/
//   pirate-pack/Sprites/Items/
//   pirate-pack/Sprites/Effects/
//   pirate-kit/Sprites/Ships/
//   pirate-kit/Sprites/Characters/
//   pirate-kit/Sprites/Items/

using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Booty.Editor
{
    /// <summary>
    /// Configures imported Kenney PNG assets as Unity 2D sprites and
    /// moves them into semantically named sub-folders.
    /// </summary>
    public class KenneyImporter : AssetPostprocessor
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const string KenneyRootPath   = "Assets/Art/Kenney";
        private const string PiratePack       = "pirate-pack";
        private const string PirateKit        = "pirate-kit";

        /// <summary>Default pixels-per-unit for all Kenney sprites.</summary>
        private const int PixelsPerUnit = 100;

        // -----------------------------------------------------------------------
        // Categorisation: filename keyword  →  sub-folder name
        // Applied in order — first match wins.
        // -----------------------------------------------------------------------
        private static readonly (string[] Keywords, string SubFolder)[] CategoryRules =
        {
            (new[] { "ship", "galleon", "frigate", "brigantine", "hull", "sail", "mast", "cannon", "anchor" }, "Ships"),
            (new[] { "pirate", "sailor", "crew", "captain", "skeleton", "parrot", "character", "person" },     "Characters"),
            (new[] { "coin", "gold", "gem", "treasure", "chest", "loot", "item", "sword", "gun", "pistol",
                     "rifle", "bomb", "barrel", "bottle", "map", "scroll" },                                   "Items"),
            (new[] { "tile", "water", "ocean", "sea", "wave", "sand", "island", "ground", "dirt", "rock",
                     "cliff", "shore", "beach", "grass" },                                                     "Tiles"),
            (new[] { "icon", "button", "ui", "hud", "cursor", "pointer", "panel", "badge", "flag", "banner" }, "UI"),
            (new[] { "explosion", "smoke", "splash", "foam", "effect", "particle", "spark", "fire" },          "Effects"),
        };

        private const string FallbackSubFolder = "Misc";

        // -----------------------------------------------------------------------
        // AssetPostprocessor — runs whenever Unity imports an asset
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called by Unity before a texture is imported.  Configures sprites
        /// inside the Kenney root automatically.
        /// </summary>
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace("\\", "/").StartsWith(KenneyRootPath, StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (TextureImporter)assetImporter;
            ConfigureSpriteImporter(importer);
        }

        // -----------------------------------------------------------------------
        // Manual menu  Booty! > Import Kenney Assets
        // -----------------------------------------------------------------------

        [MenuItem("Booty!/Import Kenney Assets")]
        public static void ImportKenneyAssets()
        {
            int moved     = 0;
            int configured = 0;

            // Enumerate all PNGs under Assets/Art/Kenney/
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { KenneyRootPath });

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Configure importer settings
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null)
                    {
                        ConfigureSpriteImporter(importer);
                        importer.SaveAndReimport();
                        configured++;
                    }

                    // Move to categorised folder
                    string newPath = GetTargetPath(path);
                    if (newPath != path)
                    {
                        EnsureDirectoryExists(Path.GetDirectoryName(newPath));
                        string error = AssetDatabase.MoveAsset(path, newPath);
                        if (string.IsNullOrEmpty(error))
                            moved++;
                        else
                            Debug.LogWarning($"[KenneyImporter] Could not move {path} → {newPath}: {error}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[KenneyImporter] Done. Configured: {configured} sprites, moved: {moved} assets.");
            EditorUtility.DisplayDialog(
                "Kenney Import Complete",
                $"Configured {configured} sprites.\nMoved {moved} assets to categorised folders.",
                "OK");
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>Sets sprite import settings for a 2D top-down game.</summary>
        private static void ConfigureSpriteImporter(TextureImporter importer)
        {
            importer.textureType           = TextureImporterType.Sprite;
            importer.spriteImportMode      = SpriteImportMode.Single;
            importer.spritePivot           = new Vector2(0.5f, 0.5f);
            importer.spritePixelsPerUnit   = PixelsPerUnit;
            importer.filterMode            = FilterMode.Point;     // crisp pixels
            importer.mipmapEnabled         = false;
            importer.alphaIsTransparency   = true;
            importer.textureCompression    = TextureImporterCompression.Uncompressed;

            // Platform override for standalone / PC
            var settings = new TextureImporterPlatformSettings
            {
                name            = "Standalone",
                overridden      = true,
                format          = TextureImporterFormat.RGBA32,
                maxTextureSize  = 2048,
            };
            importer.SetPlatformTextureSettings(settings);
        }

        /// <summary>
        /// Determines the canonical target path for an asset, based on its pack
        /// name and filename keywords.
        /// </summary>
        private static string GetTargetPath(string assetPath)
        {
            // Normalise slashes
            assetPath = assetPath.Replace("\\", "/");

            // Detect which pack this belongs to
            string packFolder = null;
            if (assetPath.Contains($"/{PiratePack}/"))
                packFolder = PiratePack;
            else if (assetPath.Contains($"/{PirateKit}/"))
                packFolder = PirateKit;

            if (packFolder == null)
                return assetPath;  // Not one of our packs — leave it alone

            // Categorise by filename
            string fileName = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            string subFolder = Categorise(fileName);

            string targetDir  = $"{KenneyRootPath}/{packFolder}/Sprites/{subFolder}";
            string targetPath = $"{targetDir}/{Path.GetFileName(assetPath)}";
            return targetPath;
        }

        /// <summary>Returns the sub-folder name for a given (lower-case) file stem.</summary>
        private static string Categorise(string fileNameLower)
        {
            foreach (var (keywords, subFolder) in CategoryRules)
            {
                foreach (string kw in keywords)
                {
                    if (fileNameLower.Contains(kw))
                        return subFolder;
                }
            }
            return FallbackSubFolder;
        }

        /// <summary>
        /// Creates an Assets folder hierarchy if it does not yet exist.
        /// Works with AssetDatabase so Unity tracks the folders.
        /// </summary>
        private static void EnsureDirectoryExists(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return;

            directory = directory.Replace("\\", "/");
            if (AssetDatabase.IsValidFolder(directory))
                return;

            // Walk the path creating each missing segment
            string[] parts  = directory.Split('/');
            string   current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // -----------------------------------------------------------------------
        // Listing utility (for CI / build verification)
        // -----------------------------------------------------------------------

        [MenuItem("Booty!/List Kenney Assets (Console)")]
        public static void ListKenneyAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { KenneyRootPath });
            var packCounts = new Dictionary<string, int>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string pack = path.Contains(PiratePack) ? PiratePack
                            : path.Contains(PirateKit)  ? PirateKit
                            : "other";
                packCounts.TryGetValue(pack, out int count);
                packCounts[pack] = count + 1;
            }

            Debug.Log("[KenneyImporter] Asset inventory:");
            foreach (var kvp in packCounts)
                Debug.Log($"  {kvp.Key}: {kvp.Value} sprites");
            Debug.Log($"  Total: {guids.Length} sprites under {KenneyRootPath}");
        }
    }
}
