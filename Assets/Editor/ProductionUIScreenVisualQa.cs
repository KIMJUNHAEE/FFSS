using System.Collections.Generic;
using System.IO;
using CardBattle;
using CardBattle.UI;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FFSS.Editor
{
    public static class ProductionUIScreenVisualQa
    {
        private const string ScreenRoot = "Assets/Prefabs/UI/Screens";

        [MenuItem("FFSS/Production/Render Run UI Visual QA")]
        public static void Render()
        {
            string output = Path.GetFullPath("Artifacts/UIQA");
            Directory.CreateDirectory(output);
            var screens = new Dictionary<string, string>
            {
                { "title", ScreenRoot + "/TitleScreen.prefab" },
                { "load", ScreenRoot + "/LoadScreen.prefab" },
                { "field_hud", ScreenRoot + "/FieldHudScreen.prefab" },
                { "equipment", ScreenRoot + "/EquipmentScreen.prefab" },
                { "shop", ScreenRoot + "/ShopScreen.prefab" },
                { "card_workshop", ScreenRoot + "/CardWorkshopScreen.prefab" },
                { "event", ScreenRoot + "/EventScreen.prefab" },
                { "break", ScreenRoot + "/BreakScreen.prefab" },
                { "reward", ScreenRoot + "/RewardScreen.prefab" },
                { "rest", ScreenRoot + "/RestScreen.prefab" },
                { "boss_door", ScreenRoot + "/BossDoorScreen.prefab" },
                { "act_transition", ScreenRoot + "/ActTransitionScreen.prefab" },
                { "run_status", ScreenRoot + "/RunStatusScreen.prefab" },
                { "options", ScreenRoot + "/OptionsScreen.prefab" },
                { "result", ScreenRoot + "/ResultScreen.prefab" }
            };

            foreach (KeyValuePair<string, string> pair in screens)
            {
                RenderPrefab(pair.Key, pair.Value, 1920, 1080, output);
                RenderPrefab(pair.Key, pair.Value, 1280, 720, output);
                RenderPrefab(pair.Key, pair.Value, 960, 540, output);
            }

            Debug.Log($"FFSS UI visual QA rendered to {output}");
        }

        [MenuItem("FFSS/Production/Render Options Tabs Visual QA")]
        public static void RenderOptionsTabs()
        {
            string output = Path.GetFullPath("Artifacts/UIQA");
            Directory.CreateDirectory(output);
            string prefabPath = ScreenRoot + "/OptionsScreen.prefab";
            string[] names = { "display", "audio", "combat", "accessibility", "controls", "data" };
            for (int page = 0; page < names.Length; page++)
                RenderPrefab("options_" + names[page], prefabPath, 1920, 1080, output, page);
            Debug.Log($"FFSS options tab visual QA rendered to {output}");
        }

        private static void RenderPrefab(
            string id,
            string prefabPath,
            int width,
            int height,
            string output,
            int optionPage = -1)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("QA Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            var canvasObject = new GameObject("QA Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            instance.transform.SetParent(canvasObject.transform, false);
            Behaviour controller = instance.GetComponent("RunUIScreenController") as Behaviour;
            if (controller != null)
            {
                if (optionPage >= 0)
                    ShowOptionPage(controller, optionPage);
                controller.enabled = false;
            }
            CardDeckExchangeScreenController deckExchange = instance.GetComponent<CardDeckExchangeScreenController>();
            if (deckExchange != null)
            {
                PopulateDeckExchangePreview(deckExchange);
                deckExchange.enabled = false;
            }
            instance.GetComponent<UIScreen>().SetVisible(true, false);

            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, $"{id}_{width}x{height}.png"), image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(texture);
        }

        private static void PopulateDeckExchangePreview(CardDeckExchangeScreenController controller)
        {
            var serialized = new SerializedObject(controller);
            Transform currentContent = serialized.FindProperty("currentDeckContent").objectReferenceValue as Transform;
            Transform ownedContent = serialized.FindProperty("ownedCardContent").objectReferenceValue as Transform;
            DeckExchangeCardSlot slotPrefab = serialized.FindProperty("cardSlotPrefab").objectReferenceValue as DeckExchangeCardSlot;
            if (currentContent == null || ownedContent == null || slotPrefab == null)
                return;

            var activeIds = new List<string>(54);
            string[] suits = { "spade", "heart", "diamond", "club" };
            foreach (string suit in suits)
            {
                for (int rank = 1; rank <= 13; rank++)
                    activeIds.Add($"poker.{suit}.{rank:D2}");
            }
            activeIds.Add("poker.joker.red");
            activeIds.Add("poker.joker.black");

            string[] ownedIds =
            {
                "poker.heart.11", "poker.club.12", "poker.diamond.01", "poker.spade.10",
                "poker.joker.red", "poker.joker.black", "poker.heart.13", "poker.club.01",
                "poker.diamond.11", "poker.spade.12", "poker.club.13", "poker.heart.01"
            };

            var active = new List<RunCardState>();
            var owned = new List<RunCardState>();
            for (int i = 0; i < activeIds.Count; i++)
            {
                var card = new RunCardState($"preview.active.{i}", activeIds[i]);
                active.Add(card);
                DeckExchangeCardSlot slot = Object.Instantiate(slotPrefab, currentContent);
                slot.Bind(card, i == 0, null);
            }
            for (int i = 0; i < ownedIds.Length; i++)
            {
                var card = new RunCardState($"preview.owned.{i}", ownedIds[i])
                {
                    enhancementLevel = i % 3 + 1,
                    growthPath = i >= 4 ? CardGrowthPath.TimeAwakened : CardGrowthPath.None
                };
                owned.Add(card);
                DeckExchangeCardSlot slot = Object.Instantiate(slotPrefab, ownedContent);
                slot.Bind(card, i == 0, null);
            }

            SetPreviewText(serialized, "currentDeckCount", $"내 덱  {active.Count} / 54");
            SetPreviewText(serialized, "ownedCardCount", $"교환 가능한 카드  {owned.Count}장");
            SetPreviewText(serialized, "selectedCurrentLabel", PokerCardPresentation.DisplayName(active[0]));
            SetPreviewText(serialized, "selectedOwnedLabel", PokerCardPresentation.DisplayName(owned[0]));
            SetPreviewArtwork(serialized, "selectedCurrentArtwork", active[0]);
            SetPreviewArtwork(serialized, "selectedOwnedArtwork", owned[0]);
            Canvas.ForceUpdateCanvases();
        }

        private static void SetPreviewText(SerializedObject serialized, string property, string value)
        {
            TMPro.TMP_Text text = serialized.FindProperty(property).objectReferenceValue as TMPro.TMP_Text;
            if (text != null)
                text.text = value;
        }

        private static void SetPreviewArtwork(SerializedObject serialized, string property, RunCardState card)
        {
            Image image = serialized.FindProperty(property).objectReferenceValue as Image;
            if (image == null)
                return;

            image.sprite = PokerCardPresentation.LoadArtwork(card);
            image.overrideSprite = image.sprite;
            image.preserveAspect = true;
            image.enabled = image.sprite != null;
        }

        private static void ShowOptionPage(Behaviour controller, int page)
        {
            var serialized = new SerializedObject(controller);
            SerializedProperty slots = serialized.FindProperty("optionSlots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                GameObject root = slot.FindPropertyRelative("root").objectReferenceValue as GameObject;
                if (root != null)
                    root.SetActive(slot.FindPropertyRelative("page").intValue == page);
            }

            SerializedProperty tabLabels = serialized.FindProperty("optionTabLabels");
            for (int i = 0; i < tabLabels.arraySize; i++)
            {
                TMPro.TMP_Text label = tabLabels.GetArrayElementAtIndex(i).objectReferenceValue as TMPro.TMP_Text;
                if (label != null)
                    label.color = i == page ? new Color(1f, 0.84f, 0.3f) : new Color(0.82f, 0.86f, 0.94f);
            }

            string[] descriptions =
            {
                "화면 표시 방식과 글자 크기를 조절해.",
                "전체·배경음악·효과음 음량을 따로 조절해.",
                "전투 연출의 움직임과 흔들림을 정해.",
                "전투 의도와 정보 대비를 더 또렷하게 만들어.",
                "방향키로 이동하고 Enter로 선택해. Esc는 현재 창을 닫아.",
                "설정은 바꾸는 즉시 저장돼. 런 저장은 런 현황에서 관리해."
            };
            TMPro.TMP_Text body = controller.transform.Find("Art Frame/Body")?.GetComponent<TMPro.TMP_Text>();
            if (body != null && page >= 0 && page < descriptions.Length)
                body.text = descriptions[page];
        }
    }
}
