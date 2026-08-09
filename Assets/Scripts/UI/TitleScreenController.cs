using System;
using System.Collections;
using System.Linq;
using CardBattle;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Persistence;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FFSS.UI
{
    public sealed class TitleScreenController : MonoBehaviour
    {
        private const string MasterVolumeKey = "settings.masterVolume";
        private const string FullscreenKey = "settings.fullscreen";

        [Header("Main menu")]
        [SerializeField] private Button newRunButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Options")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button closeOptionsButton;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("New game guide")]
        [SerializeField] private GameObject guidePanel;
        [SerializeField] private TMP_Text guideTitleText;
        [SerializeField] private TMP_Text guideBodyText;
        [SerializeField] private TMP_Text guidePageText;
        [SerializeField] private TMP_Text guideNextLabel;
        [SerializeField] private Button guidePreviousButton;
        [SerializeField] private Button guideNextButton;
        [SerializeField] private Button guideCloseButton;

        [Header("Boss debug")]
        [SerializeField] private Button bossDebugButton;
        [SerializeField] private GameObject bossDebugPanel;
        [SerializeField] private Button debugBoss13Button;
        [SerializeField] private Button debugBoss18Button;
        [SerializeField] private Button debugBoss38Button;
        [SerializeField] private Button closeBossDebugButton;

        private int guidePage;

        private static readonly string[] GuideTitles =
        {
            "빼앗긴 포커포커 마을",
            "필드와 덱 관리",
            "포커 대 섯다 전투"
        };

        private static readonly string[] GuideBodies =
        {
            "섯다 세력이 포커포커 마을을 침공했어.\n무너진 세 막을 지나 일반 적과 중간보스를 돌파하고, 막 끝의 광땡 보스를 쓰러뜨려 마을을 되찾아야 해.\nHP가 0이 되면 런이 끝나고, 전투 사이의 HP와 장비·덱 상태는 그대로 이어져.",
            "WASD 또는 방향키로 이동해.\n붉은 전투 건물, 사건 건물, 상점 건물은 가까이 가서 상호작용할 수 있어.\n필드의 덱 버튼에서 보유 카드를 확인하고 연마·성장·교환할 수 있고, 장비는 우클릭으로 즉시 장착하거나 끌어서 옮길 수 있어.",
            "매 턴 포커 카드 5장으로 행동해. 붉은 문양은 공격, 검은 문양은 방어를 강화해.\n다시 뽑기는 선택한 카드만 교체하고 나머지는 유지해. 적은 다음 행동과 섯다 전용패 효과를 미리 공개해.\n특수 족보로 스킬을 사용하면 이후 3턴 동안 다시 사용할 수 없어."
        };

        private void OnEnable()
        {
            newRunButton.onClick.AddListener(StartNewRun);
            continueButton.onClick.AddListener(ContinueRun);
            loadButton?.onClick.AddListener(OpenLoad);
            optionsButton.onClick.AddListener(OpenOptions);
            quitButton.onClick.AddListener(Quit);
            closeOptionsButton.onClick.AddListener(CloseOptions);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            guidePreviousButton?.onClick.AddListener(PreviousGuidePage);
            guideNextButton?.onClick.AddListener(NextGuidePage);
            guideCloseButton?.onClick.AddListener(CloseGuide);
            bossDebugButton?.onClick.AddListener(OpenBossDebug);
            debugBoss13Button?.onClick.AddListener(StartDebugBoss13);
            debugBoss18Button?.onClick.AddListener(StartDebugBoss18);
            debugBoss38Button?.onClick.AddListener(StartDebugBoss38);
            closeBossDebugButton?.onClick.AddListener(CloseBossDebug);
            StartCoroutine(RefreshWhenReady());
        }

        private void OnDisable()
        {
            newRunButton.onClick.RemoveListener(StartNewRun);
            continueButton.onClick.RemoveListener(ContinueRun);
            loadButton?.onClick.RemoveListener(OpenLoad);
            optionsButton.onClick.RemoveListener(OpenOptions);
            quitButton.onClick.RemoveListener(Quit);
            closeOptionsButton.onClick.RemoveListener(CloseOptions);
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
            guidePreviousButton?.onClick.RemoveListener(PreviousGuidePage);
            guideNextButton?.onClick.RemoveListener(NextGuidePage);
            guideCloseButton?.onClick.RemoveListener(CloseGuide);
            bossDebugButton?.onClick.RemoveListener(OpenBossDebug);
            debugBoss13Button?.onClick.RemoveListener(StartDebugBoss13);
            debugBoss18Button?.onClick.RemoveListener(StartDebugBoss18);
            debugBoss38Button?.onClick.RemoveListener(StartDebugBoss38);
            closeBossDebugButton?.onClick.RemoveListener(CloseBossDebug);
            StopAllCoroutines();
        }

        private void Update()
        {
            if (guidePanel != null && guidePanel.activeSelf &&
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseGuide();
            }
            else if (bossDebugPanel != null && bossDebugPanel.activeSelf &&
                     Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseBossDebug();
            }
        }

        private IEnumerator RefreshWhenReady()
        {
            while (!GameKernel.IsReady)
            {
                yield return null;
            }

            continueButton.interactable = GameKernel.Services.Get<SaveManager>().HasSave(0);
            if (loadButton != null)
            {
                SaveManager saves = GameKernel.Services.Get<SaveManager>();
                bool hasAnySave = false;
                for (int slot = 0; slot < saves.SlotCount; slot++)
                {
                    hasAnySave |= saves.HasSave(slot);
                }

                loadButton.interactable = hasAnySave;
            }
            float volume = PlayerPrefs.GetFloat(MasterVolumeKey, 0.85f);
            bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            masterVolumeSlider.SetValueWithoutNotify(volume);
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
            AudioListener.volume = volume;
            optionsPanel.SetActive(false);
            guidePanel?.SetActive(false);
            bossDebugPanel?.SetActive(false);
        }

        private void StartNewRun()
        {
            if (!GameKernel.IsReady)
            {
                return;
            }

            PlayConfirmCue();
            if (guidePanel == null)
            {
                BeginNewRun();
                return;
            }

            guidePage = 0;
            guidePanel.SetActive(true);
            RefreshGuide();
        }

        private void BeginNewRun()
        {
            int seed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Millisecond);
            guidePanel?.SetActive(false);
            GameKernel.Services.Get<RunManager>().StartNewRun(seed);
            EnterField();
        }

        private void PreviousGuidePage()
        {
            guidePage = Mathf.Max(0, guidePage - 1);
            RefreshGuide();
        }

        private void NextGuidePage()
        {
            if (guidePage >= GuideTitles.Length - 1)
            {
                BeginNewRun();
                return;
            }

            guidePage++;
            RefreshGuide();
        }

        private void CloseGuide()
        {
            guidePanel?.SetActive(false);
        }

        private void OpenBossDebug()
        {
            if (!GameKernel.IsReady)
                return;

            PlayConfirmCue();
            guidePanel?.SetActive(false);
            optionsPanel?.SetActive(false);
            bossDebugPanel?.SetActive(true);
        }

        private void CloseBossDebug()
        {
            PlayConfirmCue();
            bossDebugPanel?.SetActive(false);
        }

        private void StartDebugBoss13() => StartDebugBoss(1, "13");
        private void StartDebugBoss18() => StartDebugBoss(2, "18");
        private void StartDebugBoss38() => StartDebugBoss(3, "38");

        private void StartDebugBoss(int act, string bossId)
        {
            if (!GameKernel.IsReady)
                return;

            int seed = unchecked(Environment.TickCount ^ bossId.GetHashCode());
            RunManager runs = GameKernel.Services.Get<RunManager>();
            RunState run = runs.StartNewRun(seed);
            RunProgressionManager progression = GameKernel.Services.Get<RunProgressionManager>();
            RunActDefinition actDefinition = progression.Campaign.GetAct(act);
            run.act = act;
            run.regionId = actDefinition.regionId;
            run.gold = 999;
            run.inventoryItemIds.Clear();
            run.equippedItemIds.Clear();

            foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
            {
                EquipmentDefinition equipped = EquipmentCatalog.All
                    .Where(item => item.Slot == slot)
                    .OrderByDescending(item => item.Rarity)
                    .First();
                run.equippedItemIds.Add(equipped.Id);
            }

            foreach (EquipmentDefinition item in EquipmentCatalog.All)
            {
                if (!run.equippedItemIds.Contains(item.Id))
                    run.inventoryItemIds.Add(item.Id);
            }

            EquipmentStatsCalculator.Recalculate(run);
            run.player.currentHp = run.player.maxHp;
            run.player.currentPressure = 0;
            run.CurrentActProgress.bossDoorUnlocked = true;
            runs.NotifyStateChanged("debug.boss.ready");
            bossDebugPanel?.SetActive(false);

            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            if (!flow.TryChangeState(GameFlowState.Field) ||
                !GameKernel.Services.Get<EncounterFlowManager>()
                    .TryEnterEncounter(bossId, $"debug.boss.{bossId}.{seed}"))
            {
                Debug.LogError($"Failed to start debug boss encounter: {bossId}", this);
            }
        }

        private void RefreshGuide()
        {
            guidePage = Mathf.Clamp(guidePage, 0, GuideTitles.Length - 1);
            if (guideTitleText != null) guideTitleText.text = GuideTitles[guidePage];
            if (guideBodyText != null) guideBodyText.text = GuideBodies[guidePage];
            if (guidePageText != null) guidePageText.text = $"{guidePage + 1} / {GuideTitles.Length}";
            if (guideNextLabel != null)
                guideNextLabel.text = guidePage == GuideTitles.Length - 1 ? "게임 시작" : "다음";
            if (guidePreviousButton != null)
                guidePreviousButton.interactable = guidePage > 0;
        }

        private void ContinueRun()
        {
            if (!GameKernel.IsReady)
            {
                return;
            }

            SaveGameData loaded = GameKernel.Services.Get<SaveManager>().Load(0);
            if (loaded != null)
            {
                PlayConfirmCue();
                EnterField();
            }
        }

        private static void EnterField()
        {
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            if (flow.TryChangeState(GameFlowState.Field))
            {
                GameKernel.Services.Get<SceneFlowManager>().TryLoad(GameSceneId.Field);
            }
        }

        private void OpenOptions()
        {
            PlayConfirmCue();
            optionsPanel.SetActive(true);
        }

        private static void OpenLoad()
        {
            if (GameKernel.IsReady)
            {
                PlayConfirmCue();
                GameKernel.Services.Get<FFSS.Framework.UI.UIManager>()
                    .Show(FFSS.Framework.UI.UIScreenId.Load);
            }
        }

        private void CloseOptions()
        {
            PlayConfirmCue();
            optionsPanel.SetActive(false);
            PlayerPrefs.Save();
        }

        private static void SetMasterVolume(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        }

        private static void SetFullscreen(bool value)
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        private static void Quit()
        {
            PlayConfirmCue();
            Application.Quit();
        }

        private static void PlayConfirmCue()
        {
            if (GameKernel.IsReady && GameKernel.Services.TryGet(out AudioManager audio))
                audio.Play("sfx.card.reveal");
        }
    }
}
