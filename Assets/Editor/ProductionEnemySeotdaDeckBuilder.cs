using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardBattle;
using CardBattle.EditorTools;
using FFSS.Framework.Combat;
using UnityEditor;
using UnityEngine;

namespace FFSS.Editor
{
    public static class ProductionEnemySeotdaDeckBuilder
    {
        private const string ArtRoot = "Assets/Art/Production/Cards/EnemySeotdaDecks";
        private const string DeckRoot = "Assets/Data/Production/SeotdaDecks";
        private const string ProfileRoot = "Assets/Data/BossProfiles";
        private const string EncounterRoot = "Assets/Data/Production/Encounters";

        [MenuItem("FFSS/Production/Build Enemy Seotda Decks")]
        public static void BuildEnemySeotdaDecks()
        {
            ClockworkTimekeeperEditorUtils.EnsureFolder(DeckRoot);
            var profiles = LoadProfiles();
            string[] deckFolders = Directory.Exists(ArtRoot)
                ? Directory.GetDirectories(ArtRoot)
                : Array.Empty<string>();
            Array.Sort(deckFolders, StringComparer.Ordinal);

            int configured = 0;
            for (int i = 0; i < deckFolders.Length; i++)
            {
                string folderName = Path.GetFileName(deckFolders[i]);
                string enemyId = EnemyIdFromFolder(folderName);
                if (string.IsNullOrWhiteSpace(enemyId) || !profiles.TryGetValue(enemyId, out BossCombatProfile profile))
                {
                    continue;
                }

                string[] imagePaths = Directory.GetFiles(deckFolders[i], "*.png")
                    .Select(ToAssetPath)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                ConfigureSpriteImports(imagePaths);

                EnemySeotdaDeckDefinition deck = LoadOrCreateDeck(enemyId);
                deck.enemyId = enemyId;
                deck.displayName = profile.displayName;
                string[] folderParts = folderName.Split('_');
                deck.identity = folderParts.Length >= 3 ? folderParts[2].Replace("20장", string.Empty) : enemyId;
                deck.motif = $"{profile.displayName} 전용 1월~10월 섯다 덱";
                deck.cards.Clear();
                deck.backSprite = null;

                for (int imageIndex = 0; imageIndex < imagePaths.Length; imageIndex++)
                {
                    string imagePath = imagePaths[imageIndex];
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                    string fileName = Path.GetFileNameWithoutExtension(imagePath);
                    if (fileName.StartsWith("Back_", StringComparison.OrdinalIgnoreCase))
                    {
                        deck.backSprite = sprite;
                        continue;
                    }

                    if (!TryParseCard(fileName, out int month, out string variant))
                    {
                        continue;
                    }

                    deck.cards.Add(new EnemySeotdaDeckCardDefinition
                    {
                        cardId = fileName,
                        month = month,
                        variant = variant,
                        isGwang = variant == "A" && (month == 1 || month == 3 || month == 8),
                        faceSprite = sprite
                    });
                }

                deck.cards = deck.cards
                    .OrderBy(card => card.month)
                    .ThenBy(card => card.variant, StringComparer.Ordinal)
                    .ToList();
                EditorUtility.SetDirty(deck);

                profile.exclusiveSeotdaDeck = deck;
                EditorUtility.SetDirty(profile);
                string profileName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(profile));
                EnemyEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<EnemyEncounterDefinition>(
                    $"{EncounterRoot}/{profileName}.asset");
                if (encounter != null)
                {
                    encounter.exclusiveSeotdaDeck = deck;
                    EditorUtility.SetDirty(encounter);
                }

                if (!deck.IsConfigured)
                {
                    throw new InvalidDataException(
                        $"{enemyId}: expected 20 face cards and one back, found {deck.cards.Count} faces.");
                }
                configured++;
            }

            if (configured != profiles.Count)
            {
                throw new InvalidDataException(
                    $"Expected {profiles.Count} enemy decks but configured {configured}. Check {ArtRoot}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Configured {configured} complete enemy Seotda decks (340 faces and 17 backs). No scenes rebuilt.");
        }

        private static Dictionary<string, BossCombatProfile> LoadProfiles()
        {
            var result = new Dictionary<string, BossCombatProfile>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:BossCombatProfile", new[] { ProfileRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                BossCombatProfile profile = AssetDatabase.LoadAssetAtPath<BossCombatProfile>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (profile != null && !string.IsNullOrWhiteSpace(profile.bossId))
                {
                    result[profile.bossId] = profile;
                }
            }
            return result;
        }

        private static EnemySeotdaDeckDefinition LoadOrCreateDeck(string enemyId)
        {
            string safeName = enemyId.Replace("/", "_");
            string path = $"{DeckRoot}/{safeName}.asset";
            EnemySeotdaDeckDefinition deck = AssetDatabase.LoadAssetAtPath<EnemySeotdaDeckDefinition>(path);
            if (deck != null)
            {
                return deck;
            }

            deck = ScriptableObject.CreateInstance<EnemySeotdaDeckDefinition>();
            AssetDatabase.CreateAsset(deck, path);
            return deck;
        }

        private static void ConfigureSpriteImports(IEnumerable<string> imagePaths)
        {
            foreach (string imagePath in imagePaths)
            {
                if (AssetImporter.GetAtPath(imagePath) is not TextureImporter importer)
                {
                    continue;
                }

                bool changed = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               importer.mipmapEnabled;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 1024;
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static string EnemyIdFromFolder(string folderName)
        {
            string[] parts = folderName.Split('_');
            if (parts.Length < 2)
            {
                return string.Empty;
            }

            string token = parts[1];
            return token.EndsWith("광땡", StringComparison.Ordinal)
                ? token.Substring(0, token.Length - "광땡".Length)
                : token;
        }

        private static bool TryParseCard(string fileName, out int month, out string variant)
        {
            month = 0;
            variant = string.Empty;
            string[] parts = fileName.Split('_');
            if (parts.Length < 2)
            {
                return false;
            }

            string monthToken = parts[0];
            int digits = 0;
            while (digits < monthToken.Length && char.IsDigit(monthToken[digits]))
            {
                digits++;
            }
            if (digits == 0 || !int.TryParse(monthToken.Substring(0, digits), out month))
            {
                return false;
            }

            variant = parts[1];
            return month >= 1 && month <= 10 && (variant == "A" || variant == "B");
        }

        private static string ToAssetPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            int assetsIndex = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return assetsIndex >= 0 ? normalized.Substring(assetsIndex + 1) : normalized;
        }
    }
}
