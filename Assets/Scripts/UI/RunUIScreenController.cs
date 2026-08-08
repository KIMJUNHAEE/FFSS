using System;
using System.Collections;
using System.Collections.Generic;
using CardBattle.Inventory;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Persistence;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using Text = TMPro.TMP_Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CardBattle.UI
{
    [Serializable]
    public sealed class RunScreenActionSlot
    {
        public Button button;
        public Text label;
        public Text detail;
        public Image icon;
    }

    public enum RunOptionBinding
    {
        Fullscreen,
        TextScale,
        MasterVolume,
        MusicVolume,
        EffectsVolume,
        ReduceMotion,
        ScreenShake,
        HighContrast,
        ControlsInfo,
        DataInfo
    }

    [Serializable]
    public sealed class RunScreenOptionSlot
    {
        public GameObject root;
        public int page;
        public RunOptionBinding binding;
        public Text label;
        public Text value;
        public Toggle toggle;
        public Slider slider;
    }

    [DisallowMultipleComponent]
    public sealed class RunUIScreenController : MonoBehaviour
    {
        [Header("Screen")]
        [SerializeField] private UIScreen screen;
        [SerializeField] private Text heading;
        [SerializeField] private Text subtitle;
        [SerializeField] private Text body;
        [SerializeField] private Text currency;
        [SerializeField] private Text status;
        [SerializeField] private Slider hpGauge;
        [SerializeField] private Slider pressureGauge;
        [SerializeField] private Image hpGaugeFill;
        [SerializeField] private Image pressureGaugeFill;
        [SerializeField] private Text hpGaugeText;
        [SerializeField] private Text attackValueText;
        [SerializeField] private Text defenseValueText;

        [Header("Commands")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private Text secondaryLabel;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private List<RunScreenActionSlot> actions = new List<RunScreenActionSlot>();

        [Header("Options")]
        [SerializeField] private List<Button> optionTabs = new List<Button>();
        [SerializeField] private List<Text> optionTabLabels = new List<Text>();
        [SerializeField] private List<RunScreenOptionSlot> optionSlots = new List<RunScreenOptionSlot>();

        private readonly List<UnityAction> actionCallbacks = new List<UnityAction>();
        private readonly List<UnityAction> optionTabCallbacks = new List<UnityAction>();
        private readonly List<UnityAction<bool>> optionToggleCallbacks = new List<UnityAction<bool>>();
        private readonly List<UnityAction<float>> optionSliderCallbacks = new List<UnityAction<float>>();
        private string sourceId;
        private string progressNodeId;
        private int selectedAction;
        private int page;
        private Coroutine refreshRoutine;
        private IDisposable screenShownSubscription;
        private IDisposable runStateChangedSubscription;
        private int selectedOptionTab;
        private int lastSavedSlot = -1;
        private DateTime lastSavedAt;

        public UIScreenId ScreenId => screen != null ? screen.Id : UIScreenId.Title;
        public string SourceId => sourceId;

        public void Configure(string contextId)
        {
            DecodeContext(contextId, out progressNodeId, out sourceId);
            selectedAction = 0;
            if (isActiveAndEnabled && GameKernel.IsReady)
            {
                Refresh();
            }
        }

        private void Awake()
        {
            if (screen == null)
            {
                screen = GetComponent<UIScreen>();
            }
        }

        private void OnEnable()
        {
            closeButton?.onClick.AddListener(Close);
            primaryButton?.onClick.AddListener(Primary);
            secondaryButton?.onClick.AddListener(Secondary);
            previousPageButton?.onClick.AddListener(PreviousPage);
            nextPageButton?.onClick.AddListener(NextPage);
            actionCallbacks.Clear();
            for (int i = 0; i < actions.Count; i++)
            {
                int index = i;
                UnityAction callback = () => ActivateAction(index);
                actionCallbacks.Add(callback);
                actions[i].button?.onClick.AddListener(callback);
            }

            WireOptionControls();

            refreshRoutine = StartCoroutine(RefreshWhenReady());
            StartCoroutine(SelectDefaultControlNextFrame());
        }

        private void OnDisable()
        {
            closeButton?.onClick.RemoveListener(Close);
            primaryButton?.onClick.RemoveListener(Primary);
            secondaryButton?.onClick.RemoveListener(Secondary);
            previousPageButton?.onClick.RemoveListener(PreviousPage);
            nextPageButton?.onClick.RemoveListener(NextPage);
            for (int i = 0; i < actions.Count && i < actionCallbacks.Count; i++)
            {
                actions[i].button?.onClick.RemoveListener(actionCallbacks[i]);
            }

            actionCallbacks.Clear();
            UnwireOptionControls();
            screenShownSubscription?.Dispose();
            screenShownSubscription = null;
            runStateChangedSubscription?.Dispose();
            runStateChangedSubscription = null;
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }
        }

        private IEnumerator RefreshWhenReady()
        {
            while (!GameKernel.IsReady)
            {
                yield return null;
            }

            screenShownSubscription?.Dispose();
            screenShownSubscription = GameKernel.Events.Subscribe<UIScreenShownEvent>(HandleScreenShown);
            runStateChangedSubscription?.Dispose();
            runStateChangedSubscription = GameKernel.Events.Subscribe<RunStateChangedEvent>(HandleRunStateChanged);
            Refresh();
            refreshRoutine = null;
        }

        private void HandleScreenShown(UIScreenShownEvent message)
        {
            if (message.Screen == ScreenId && isActiveAndEnabled)
            {
                Refresh();
            }
        }

        private void HandleRunStateChanged(RunStateChangedEvent message)
        {
            if (isActiveAndEnabled && message.State != null)
                Refresh();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;
            if (closeButton != null && closeButton.gameObject.activeInHierarchy && ScreenId != UIScreenId.Reward)
                Close();
        }

        public void Refresh()
        {
            switch (ScreenId)
            {
                case UIScreenId.Load:
                    RefreshLoad();
                    break;
                case UIScreenId.FieldHud:
                    RefreshFieldHud();
                    break;
                case UIScreenId.FieldMap:
                    RefreshFieldMap();
                    break;
                case UIScreenId.Equipment:
                    RefreshEquipment();
                    break;
                case UIScreenId.Shop:
                    RefreshShop();
                    break;
                case UIScreenId.CardWorkshop:
                    RefreshWorkshop();
                    break;
                case UIScreenId.Event:
                    RefreshEvent();
                    break;
                case UIScreenId.Reward:
                    RefreshReward();
                    break;
                case UIScreenId.Rest:
                    RefreshRest();
                    break;
                case UIScreenId.BossDoor:
                    RefreshBossDoor();
                    break;
                case UIScreenId.ActTransition:
                    RefreshActTransition();
                    break;
                case UIScreenId.RunStatus:
                    RefreshRunStatus();
                    break;
                case UIScreenId.Options:
                    RefreshOptions();
                    break;
                case UIScreenId.Result:
                    RefreshResult();
                    break;
                case UIScreenId.Break:
                    SetText(status, "상대의 자세가 무너졌다. 다음 교환에서 주도권을 잡는다.");
                    break;
            }
        }

        private void RefreshLoad()
        {
            SaveManager saves = GameKernel.Services.Get<SaveManager>();
            for (int i = 0; i < actions.Count; i++)
            {
                if (i >= saves.SlotCount)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                SaveGameData data = saves.Peek(i);
                string label = data == null ? $"슬롯 {i + 1} · 비어 있음" : $"슬롯 {i + 1} · 제{data.run.act}막";
                string detail = data == null
                    ? "새 런을 시작하면 이곳에 저장할 수 있다."
                    : $"HP {data.run.player.currentHp}/{data.run.player.maxHp}  ·  {data.run.gold}냥  ·  {FormatTime(data.run.elapsedSeconds)}";
                SetAction(i, label, detail, data != null);
            }
        }

        private void RefreshFieldHud()
        {
            RunState run = CurrentRun();
            if (run == null)
            {
                return;
            }

            SetText(heading, $"제{run.act}막");
            SetText(subtitle, FieldRegionName(run));
            SetText(currency, $"{run.gold}냥");
            string risk = run.act switch
            {
                1 => "경계",
                2 => "위험",
                _ => "극위험"
            };
            SetText(status,
                $"위험도: {risk}  ·  전투 {run.CurrentActProgress.normalVictories}/{run.CurrentActProgress.requiredNormalVictories}  ·  사건 {run.CurrentActProgress.completedEvents}/{run.CurrentActProgress.requiredEvents}");
            SetGauge(hpGauge, run.player.currentHp, run.player.maxHp);
            SetGauge(pressureGauge, run.player.currentPressure, run.player.maxPressure);
            SetGauge(hpGaugeFill, run.player.currentHp, run.player.maxHp);
            SetGauge(pressureGaugeFill, run.player.currentPressure, run.player.maxPressure);
            SetText(hpGaugeText, $"HP {run.player.currentHp} / {run.player.maxHp}");
            SetText(attackValueText, run.player.AttackForTurn(2).ToString());
            SetText(defenseValueText, run.player.DefenseForTurn(2).ToString());
        }

        private static string FieldRegionName(RunState run)
        {
            if (GameKernel.Services.TryGet(out RunProgressionManager progression))
            {
                RunActDefinition act = progression.Campaign.GetAct(run.act);
                if (!string.IsNullOrWhiteSpace(act.displayName))
                {
                    int separator = act.displayName.IndexOf(':');
                    return separator >= 0
                        ? act.displayName.Substring(separator + 1).Trim()
                        : act.displayName.Trim();
                }
            }

            return run.regionId switch
            {
                "act1_north_gate" => "북문 패거리",
                "act2_poison_canal" => "독수로",
                "act3_ruined_palace" => "무너진 궁",
                _ => run.regionId
            };
        }

        private void RefreshFieldMap()
        {
            RunState run = CurrentRun();
            if (run == null)
            {
                return;
            }

            RunActProgressState act = run.CurrentActProgress;
            act.visitedTileIds ??= new List<string>();
            string position = act.hasCurrentCell
                ? $"현재 [{act.currentAxialX}, {act.currentAxialY}]"
                : "현재 위치 확인 중";
            SetText(subtitle,
                $"{position}  ·  걸어 본 타일 {act.visitedTileIds.Count}/{act.generatedTileCount}  ·  발견 건물 {run.discoveredNodeIds.Count}");
            SetText(body, act.bossDoorUnlocked
                ? "권장 준비를 마쳤다. 지도 끝의 붉은 보스문으로 향하자."
                : $"권장 준비: 전투 {act.normalVictories}/{act.requiredNormalVictories}, 사건 {act.completedEvents}/{act.requiredEvents}, 중간보스 {(act.midBossDefeated ? "완료" : "미완료")} · 준비 전에도 입장 가능");
            string[] labels = { "전투", "사건", "상점", "보급", "보스문" };
            for (int i = 0; i < actions.Count; i++)
            {
                SetAction(i, i < labels.Length ? labels[i] : string.Empty, MapCountDetail(run, i), i < labels.Length);
            }
        }

        private void RefreshEquipment()
        {
            RunState run = CurrentRun();
            if (run == null)
            {
                return;
            }

            SetText(subtitle, $"공격 {run.player.AttackForTurn(2)}  ·  방어 {run.player.DefenseForTurn(2)}  ·  격파 {run.player.baseBreakPower}");
            SetText(body, $"장착 {run.equippedItemIds.Count}/4  ·  보유 {run.inventoryItemIds.Count}");
            for (int i = 0; i < actions.Count; i++)
            {
                string id = i < run.equippedItemIds.Count ? run.equippedItemIds[i] : string.Empty;
                EquipmentDefinition item = EquipmentCatalog.Get(id);
                string name = item != null ? item.DisplayName : "빈 슬롯";
                string detail = item != null ? item.EffectText : "이 슬롯에 맞는 장비가 없다.";
                SetAction(i, $"{EquipmentSlotName(i)} · {name}", detail, i < 4);
            }
        }

        private void RefreshShop()
        {
            RunState run = CurrentRun();
            RunEconomyManager economy = GameKernel.Services.Get<RunEconomyManager>();
            string shopId = string.IsNullOrWhiteSpace(sourceId) ? $"shop.act{run.act}.main" : sourceId;
            sourceId = shopId;
            RunShopState shop = economy.GetOrCreateShop(shopId);
            SetText(currency, $"보유 {run.gold}냥");
            SetText(body, string.Empty);
            for (int i = 0; i < actions.Count; i++)
            {
                if (i >= shop.stockIds.Count)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                RunShopOfferDefinition offer = economy.Catalog.GetOffer(shop.stockIds[i]);
                bool sold = shop.purchasedIds.Contains(offer.offerId);
                EquipmentDefinition equipment = EquipmentCatalog.Get(offer.contentId);
                if (equipment == null)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                SetVisualAction(i, !sold);
                string availability = sold ? "판매 완료" : $"가격 {offer.price}냥";
                string details =
                    $"{EquipmentSlotLabel(equipment.Slot)} · {EquipmentCatalog.RarityLabel(equipment.Rarity)}\n" +
                    $"{equipment.Description}\n\n{equipment.EffectText}\n\n{availability}";
                SetActionArtwork(i, equipment.Icon, equipment.DisplayName, details);
                if (actions[i].icon != null)
                    actions[i].icon.color = sold ? new Color(0.42f, 0.42f, 0.42f, 0.74f) : Color.white;
            }
        }

        private void RefreshWorkshop()
        {
            RunState run = CurrentRun();
            SetText(currency, $"보유 {run.gold}냥");
            SetText(subtitle, $"54장 원형 덱 · 현재 {run.pokerDeck.cards.Count}장 · 추가 다시뽑기 {run.pokerDeck.bonusRedraws}/2");
            int first = page * Mathf.Max(1, actions.Count);
            for (int i = 0; i < actions.Count; i++)
            {
                int cardIndex = first + i;
                if (cardIndex >= run.pokerDeck.cards.Count)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                RunCardState card = run.pokerDeck.cards[cardIndex];
                SetAction(i, card.cardId, $"연마 +{card.enhancementLevel} · {card.growthPath}", true);
            }

            int pages = Mathf.Max(1, Mathf.CeilToInt(run.pokerDeck.cards.Count / (float)Mathf.Max(1, actions.Count)));
            SetText(status, $"{page + 1}/{pages}쪽 · 카드를 선택한 뒤 연마하거나 시간 각성·역행 경로를 정한다.");
            if (previousPageButton != null)
            {
                previousPageButton.interactable = page > 0;
            }
            if (nextPageButton != null)
            {
                nextPageButton.interactable = page + 1 < pages;
            }
        }

        private void RefreshEvent()
        {
            RunState run = CurrentRun();
            RunContentCatalog catalog = GameKernel.Services.Get<RunEconomyManager>().Catalog;
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                for (int i = 0; i < catalog.Events.Count; i++)
                {
                    RunEventDefinition candidate = catalog.Events[i];
                    if (candidate.act == run.act && !run.completedEventIds.Contains(candidate.eventId))
                    {
                        sourceId = candidate.eventId;
                        break;
                    }
                }
            }

            RunEventDefinition definition = catalog.GetEvent(sourceId);
            SetText(heading, definition.title);
            SetText(body, definition.situation);
            SetText(currency, $"보유 {run.gold}냥");
            for (int i = 0; i < actions.Count; i++)
            {
                if (i >= definition.choices.Count)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                RunEventChoiceDefinition choice = definition.choices[i];
                string cost = choice.goldCost > 0 ? $" · {choice.goldCost}냥" : string.Empty;
                SetAction(i, choice.label + cost, choice.consequencePreview, run.gold >= choice.goldCost);
            }
        }

        private void RefreshReward()
        {
            RunState run = CurrentRun();
            RunRewardState reward = run.pendingReward;
            SetText(currency, reward == null ? "보상 없음" : $"{reward.gold}냥 획득");
            SetText(body, "장비를 선택하거나 카드 한 장을 연마하고, 다음 지역으로 돌아간다.");
            int itemCount = reward?.itemChoiceIds.Count ?? 0;
            int cardCount = reward?.cardChoiceInstanceIds?.Count ?? 0;
            for (int i = 0; i < actions.Count; i++)
            {
                if (i < itemCount)
                {
                    string itemId = reward.itemChoiceIds[i];
                    EquipmentDefinition item = EquipmentCatalog.Get(itemId);
                    string label = item != null ? item.DisplayName : itemId;
                    string detail = item != null ? $"{EquipmentCatalog.RarityLabel(item.Rarity)} 장비 · {item.EffectText}" : "장비 보상";
                    SetAction(i, label, detail, true);
                    SetActionArtwork(i, item?.Icon, label,
                        item != null ? $"{item.Description}\n\n{item.EffectText}" : detail);
                }
                else if (i - itemCount < cardCount)
                {
                    string cardInstanceId = reward.cardChoiceInstanceIds[i - itemCount];
                    RunCardState card = run.pokerDeck.FindCard(cardInstanceId);
                    if (card == null)
                    {
                        SetAction(i, string.Empty, string.Empty, false);
                        continue;
                    }

                    string label = PokerCardPresentation.DisplayName(card.cardId);
                    string detail = $"카드 연마 +{card.enhancementLevel} → +{card.enhancementLevel + 1}";
                    SetAction(i, label, detail, true);
                    SetActionArtwork(i, PokerCardPresentation.LoadArtwork(card), label,
                        $"{detail}\n\n{PokerCardPresentation.Detail(card)}");
                }
                else
                {
                    SetAction(i, string.Empty, string.Empty, false);
                }
            }
        }

        private void RefreshRest()
        {
            RunState run = CurrentRun();
            RunContentCatalog catalog = GameKernel.Services.Get<RunEconomyManager>().Catalog;
            sourceId = string.IsNullOrWhiteSpace(sourceId) ? $"rest.act{run.act}.main" : sourceId;
            SetText(subtitle, $"HP {run.player.currentHp}/{run.player.maxHp}  ·  압박 {run.player.currentPressure}/{run.player.maxPressure}");
            for (int i = 0; i < actions.Count; i++)
            {
                if (i >= catalog.RestOptions.Count)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                RunRestOptionDefinition option = catalog.RestOptions[i];
                SetAction(i, option.displayName, option.description, !run.consumedRestIds.Contains(sourceId));
            }
        }

        private void RefreshBossDoor()
        {
            RunState run = CurrentRun();
            RunCampaignDefinition campaign = GameKernel.Services.Get<RunProgressionManager>().Campaign;
            RunActDefinition act = campaign.GetAct(run.act);
            RunActProgressState actProgress = run.CurrentActProgress;
            RunProgressionManager progression = GameKernel.Services.Get<RunProgressionManager>();
            bool canEnter = progression.CanChallengeBoss(run);
            bool prepared = RunProgressionManager.MeetsBossRequirements(run);
            SetText(heading, $"{act.bossId} 보스문");
            SetText(subtitle, prepared ? "입장 가능" : "입장 가능 · 전력 부족");
            string supplyStatus = actProgress.supplyVisits > 0
                ? $"보급 {actProgress.supplyVisits}/{Mathf.Max(1, actProgress.plannedSupplyCount)} 이용"
                : $"보급 미이용 · 발견 시 최대 {Mathf.Max(1, actProgress.plannedSupplyCount)}곳";
            SetText(body, prepared
                ? $"최종 장비와 덱을 확인하자. {supplyStatus}. 문을 넘으면 보스 전투가 시작된다."
                : $"권장 전투·사건·중간보스를 마치지 않았다. {supplyStatus}. 그래도 바로 도전할 수 있다.");
            if (primaryButton != null)
            {
                primaryButton.interactable = canEnter;
            }
        }

        private void RefreshActTransition()
        {
            RunState run = CurrentRun();
            RunCampaignDefinition campaign = GameKernel.Services.Get<RunProgressionManager>().Campaign;
            RunActDefinition act = campaign.GetAct(run.act);
            bool isFinalAct = run.act >= campaign.Acts.Count;
            string restId = RunProgressionManager.IntermissionRestId(run.act);
            bool consumedRest = run.consumedRestIds.Contains(restId);

            SetText(heading, isFinalAct ? "최종 판 돌파" : $"제{run.act}막 돌파");
            SetText(subtitle, isFinalAct ? $"{act.bossId} 격파" : "막 사이 휴식 · 정비");
            SetText(body, isFinalAct
                ? $"{act.actRewardGold}냥을 받고 정산으로 넘어간다."
                : $"{act.actRewardGold}냥을 받고 다음 막에 들어가기 전 한 가지만 고른다.\n선택 없이 진행하면 기본 휴식으로 HP {act.transitionHealPercent}%를 회복한다.");
            SetText(primaryLabel, isFinalAct ? "정산 보기" : "다음 막으로");

            RunContentCatalog catalog = GameKernel.Services.Get<RunEconomyManager>().Catalog;
            for (int i = 0; i < actions.Count; i++)
            {
                if (isFinalAct || i >= catalog.RestOptions.Count)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                RunRestOptionDefinition option = catalog.RestOptions[i];
                SetAction(i, option.displayName, option.description, !consumedRest);
            }
        }

        private void RefreshRunStatus()
        {
            RunState run = CurrentRun();
            SaveManager saves = GameKernel.Services.Get<SaveManager>();
            SetText(subtitle, $"제{run.act}막 · {FormatTime(run.elapsedSeconds)} · 시드 {run.seed}");
            SetText(body, $"HP {run.player.currentHp}/{run.player.maxHp}\n공격 {run.player.AttackForTurn(2)} · 방어 {run.player.DefenseForTurn(2)} · 압박 {run.player.currentPressure}/{run.player.maxPressure}\n덱 {run.pokerDeck.cards.Count}장 · 장비 {run.equippedItemIds.Count + run.inventoryItemIds.Count}개");
            for (int i = 0; i < actions.Count; i++)
            {
                SaveGameData saved = i < saves.SlotCount ? saves.Peek(i) : null;
                string detail = saved?.run == null
                    ? (i == 0 ? "자동 저장 · 비어 있음" : "수동 저장 · 비어 있음")
                    : $"제{saved.run.act}막 · HP {saved.run.player.currentHp}/{saved.run.player.maxHp} · {FormatSavedAt(saved.savedAtUtc)}";
                SetAction(i, $"슬롯 {i + 1}에 저장", detail, i < 3);
            }

            SetText(status, lastSavedSlot >= 0
                ? $"저장 완료 · 슬롯 {lastSavedSlot + 1} · {lastSavedAt:HH:mm:ss}"
                : "저장할 슬롯을 선택해.");
        }

        private void RefreshOptions()
        {
            PlayerSettingsData settings = PlayerSettingsData.FromPreferences();
            string[] descriptions =
            {
                "화면 표시 방식과 글자 크기를 조절해.",
                "전체·배경음악·효과음 음량을 따로 조절해.",
                "전투 연출의 움직임과 흔들림을 정해.",
                "전투 의도와 정보 대비를 더 또렷하게 만들어.",
                "방향키로 이동하고 Enter로 선택해. Esc는 현재 창을 닫아.",
                "설정은 바꾸는 즉시 저장돼. 런 저장은 런 현황에서 관리해."
            };
            selectedOptionTab = Mathf.Clamp(selectedOptionTab, 0, descriptions.Length - 1);
            SetText(body, descriptions[selectedOptionTab]);

            for (int i = 0; i < optionTabLabels.Count; i++)
            {
                if (optionTabLabels[i] != null)
                    optionTabLabels[i].color = i == selectedOptionTab
                        ? new Color(1f, 0.84f, 0.3f)
                        : new Color(0.82f, 0.86f, 0.94f);
            }

            for (int i = 0; i < optionSlots.Count; i++)
            {
                RunScreenOptionSlot slot = optionSlots[i];
                if (slot == null)
                    continue;
                slot.root?.SetActive(slot.page == selectedOptionTab);
                RefreshOptionSlot(slot, settings);
            }
        }

        private void RefreshResult()
        {
            RunState run = CurrentRun();
            if (run == null)
            {
                return;
            }

            bool victory = run.outcome == RunOutcome.Victory;
            SetText(heading, victory ? "판을 끝냈다" : "이번 판은 여기까지");
            SetText(subtitle, victory ? "38광땡 격파" : run.result.causeId);
            SetText(body, $"도달: 제{run.act}막\n격파한 적: {run.encounterIndex}\n최종 덱: {run.pokerDeck.cards.Count}장\n남은 골드: {run.gold}냥");
        }

        private void ActivateAction(int index)
        {
            selectedAction = index;
            switch (ScreenId)
            {
                case UIScreenId.Load:
                    LoadSlot(index);
                    return;
                case UIScreenId.Shop:
                    Purchase(index);
                    return;
                case UIScreenId.Event:
                    ChooseEvent(index);
                    return;
                case UIScreenId.Rest:
                    ChooseRest(index);
                    return;
                case UIScreenId.ActTransition:
                    ChooseActTransitionRest(index);
                    return;
                case UIScreenId.RunStatus:
                    SaveSlot(index);
                    return;
                case UIScreenId.Options:
                    return;
                case UIScreenId.FieldHud:
                    OpenFieldCommand(index);
                    return;
                case UIScreenId.FieldMap:
                    SelectMapCategory(index);
                    return;
                case UIScreenId.Equipment:
                    CycleEquipment(index);
                    return;
                case UIScreenId.CardWorkshop:
                    selectedAction = page * Mathf.Max(1, actions.Count) + index;
                    Refresh();
                    return;
                case UIScreenId.Reward:
                    Refresh();
                    return;
            }
        }

        private void Primary()
        {
            switch (ScreenId)
            {
                case UIScreenId.BossDoor:
                    EnterBoss();
                    break;
                case UIScreenId.ActTransition:
                    if (GameKernel.Services.Get<RunProgressionManager>().CompleteAct())
                    {
                        EnterFieldOrResult();
                    }
                    break;
                case UIScreenId.Result:
                    ReturnToTitle();
                    break;
                case UIScreenId.Break:
                    GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Combat);
                    Close();
                    break;
                case UIScreenId.Reward:
                    ClaimReward();
                    break;
                case UIScreenId.CardWorkshop:
                    UpgradeSelectedCard();
                    break;
                default:
                    Close();
                    break;
            }
        }

        private void Secondary()
        {
            if (ScreenId == UIScreenId.CardWorkshop)
            {
                ChooseSelectedCardGrowth();
            }
            else if (ScreenId == UIScreenId.FieldMap)
            {
                Show(UIScreenId.RunStatus);
            }
            else if (ScreenId == UIScreenId.RunStatus)
            {
                Show(UIScreenId.Options);
            }
            else
            {
                Close();
            }
        }

        private void PreviousPage()
        {
            page = Mathf.Max(0, page - 1);
            selectedAction = page * Mathf.Max(1, actions.Count);
            Refresh();
        }

        private void NextPage()
        {
            RunState run = CurrentRun();
            int pages = Mathf.Max(1, Mathf.CeilToInt(run.pokerDeck.cards.Count / (float)Mathf.Max(1, actions.Count)));
            page = Mathf.Min(pages - 1, page + 1);
            selectedAction = page * Mathf.Max(1, actions.Count);
            Refresh();
        }

        private void UpgradeSelectedCard()
        {
            RunState run = CurrentRun();
            if (selectedAction >= 0 && selectedAction < run.pokerDeck.cards.Count)
            {
                GameKernel.Services.Get<RunEconomyManager>()
                    .TryUpgradeCard(run.pokerDeck.cards[selectedAction].instanceId, 20);
                Refresh();
            }
        }

        private void ChooseSelectedCardGrowth()
        {
            RunState run = CurrentRun();
            if (selectedAction < 0 || selectedAction >= run.pokerDeck.cards.Count)
            {
                return;
            }

            RunCardState card = run.pokerDeck.cards[selectedAction];
            CardGrowthPath next = card.growthPath == CardGrowthPath.TimeAwakened
                ? CardGrowthPath.Reverse
                : CardGrowthPath.TimeAwakened;
            GameKernel.Services.Get<RunEconomyManager>().TryChooseGrowthPath(card.instanceId, next, 30);
            Refresh();
        }

        private void LoadSlot(int slot)
        {
            SaveGameData data = GameKernel.Services.Get<SaveManager>().Load(slot);
            if (data != null)
            {
                EnterField();
            }
        }

        private void SaveSlot(int slot)
        {
            GameKernel.Services.Get<SaveManager>().Save(slot);
            lastSavedSlot = slot;
            lastSavedAt = DateTime.Now;
            RefreshRunStatus();
        }

        private void Purchase(int index)
        {
            RunEconomyManager economy = GameKernel.Services.Get<RunEconomyManager>();
            RunShopState shop = economy.GetOrCreateShop(sourceId);
            if (index < shop.stockIds.Count)
            {
                RunPurchaseResult result = economy.TryPurchaseDetailed(sourceId, shop.stockIds[index]);
                Refresh();
                SetText(status, result switch
                {
                    RunPurchaseResult.Purchased => "구매했다. 장비 가방에서 바로 확인할 수 있다.",
                    RunPurchaseResult.InsufficientGold => "엽전이 부족하다.",
                    RunPurchaseResult.AlreadyOwned => "이미 보유하거나 장착한 장비다.",
                    RunPurchaseResult.AlreadyPurchased => "이미 판매가 끝난 물건이다.",
                    _ => "지금은 구매할 수 없는 물건이다."
                });
            }
        }

        private void ChooseEvent(int index)
        {
            RunEventDefinition definition = GameKernel.Services.Get<RunEconomyManager>().Catalog.GetEvent(sourceId);
            if (index < definition.choices.Count &&
                GameKernel.Services.Get<RunEconomyManager>().ResolveEvent(sourceId, definition.choices[index].choiceId))
            {
                ResolveProgressNode();
                CloseToField();
            }
        }

        private void ChooseRest(int index)
        {
            IReadOnlyList<RunRestOptionDefinition> options = GameKernel.Services.Get<RunEconomyManager>().Catalog.RestOptions;
            if (index < options.Count &&
                GameKernel.Services.Get<RunEconomyManager>().UseRest(sourceId, options[index].type))
            {
                ResolveProgressNode();
                CloseToField();
            }
        }

        private void ChooseActTransitionRest(int index)
        {
            RunState run = CurrentRun();
            if (run == null)
            {
                return;
            }

            RunCampaignDefinition campaign = GameKernel.Services.Get<RunProgressionManager>().Campaign;
            if (run.act >= campaign.Acts.Count)
            {
                return;
            }

            IReadOnlyList<RunRestOptionDefinition> options = GameKernel.Services.Get<RunEconomyManager>().Catalog.RestOptions;
            if (index < options.Count &&
                GameKernel.Services.Get<RunEconomyManager>()
                    .UseRest(RunProgressionManager.IntermissionRestId(run.act), options[index].type))
            {
                SetText(status, $"{options[index].displayName} 완료. 다음 막으로 넘어갈 수 있다.");
                Refresh();
            }
        }

        private void OpenFieldCommand(int index)
        {
            switch (index)
            {
                case 0:
                    Show(UIScreenId.FieldMap);
                    break;
                case 1:
                    InventoryScreenController.Open();
                    break;
                case 2:
                    Show(UIScreenId.RunStatus);
                    break;
            }
        }

        private void SelectMapCategory(int index)
        {
            RunState run = CurrentRun();
            if (run == null)
            {
                return;
            }

            RunFieldContentType type = MapContentType(index);
            string label = MapCategoryName(type);
            List<RunFieldNodeState> nodes = run.CurrentActProgress.fieldNodes
                .FindAll(node => node != null && node.contentType == type && node.discovered);
            nodes.Sort((left, right) =>
            {
                int resolvedOrder = left.resolved.CompareTo(right.resolved);
                if (resolvedOrder != 0)
                {
                    return resolvedOrder;
                }

                int yOrder = right.axialY.CompareTo(left.axialY);
                return yOrder != 0 ? yOrder : left.axialX.CompareTo(right.axialX);
            });

            if (nodes.Count == 0)
            {
                SetText(body, $"아직 발견한 {label} 건물이 없다. 직접 걸어가 시야에 넣어야 지도에 기록된다.");
                SetText(status, $"{label} · 발견 0");
                return;
            }

            var lines = new List<string>(Mathf.Min(5, nodes.Count));
            for (int i = 0; i < nodes.Count && i < 5; i++)
            {
                RunFieldNodeState node = nodes[i];
                string state = node.resolved ? "완료" : node.visited ? "방문" : "미확인";
                lines.Add($"{i + 1}. {label}  [{node.axialX}, {node.axialY}]  ·  {state}");
            }

            SetText(body, string.Join("\n", lines));
            SetText(status, $"{label} · 발견 {nodes.Count} · 지도는 위치 확인용이며 순간이동하지 않는다");
        }

        private void SetOptionTab(int index)
        {
            selectedOptionTab = Mathf.Clamp(index, 0, Mathf.Max(0, optionTabs.Count - 1));
            RefreshOptions();
            if (EventSystem.current != null && selectedOptionTab < optionTabs.Count)
                EventSystem.current.SetSelectedGameObject(optionTabs[selectedOptionTab].gameObject);
        }

        private void SetOptionToggle(RunOptionBinding binding, bool value)
        {
            PlayerSettingsData settings = PlayerSettingsData.FromPreferences();
            switch (binding)
            {
                case RunOptionBinding.Fullscreen:
                    settings.fullscreen = value;
                    break;
                case RunOptionBinding.ReduceMotion:
                    settings.reduceMotion = value;
                    break;
                case RunOptionBinding.ScreenShake:
                    settings.screenShake = value;
                    break;
                case RunOptionBinding.HighContrast:
                    settings.highContrastIntents = value;
                    break;
                default:
                    return;
            }

            settings.Apply(true);
            RefreshOptions();
        }

        private void SetOptionSlider(RunOptionBinding binding, float value)
        {
            PlayerSettingsData settings = PlayerSettingsData.FromPreferences();
            switch (binding)
            {
                case RunOptionBinding.TextScale:
                    settings.textScale = Mathf.Clamp(value, 0.85f, 1.5f);
                    break;
                case RunOptionBinding.MasterVolume:
                    settings.masterVolume = Mathf.Clamp01(value);
                    break;
                case RunOptionBinding.MusicVolume:
                    settings.musicVolume = Mathf.Clamp01(value);
                    break;
                case RunOptionBinding.EffectsVolume:
                    settings.effectsVolume = Mathf.Clamp01(value);
                    settings.interfaceVolume = settings.effectsVolume;
                    break;
                default:
                    return;
            }

            settings.Apply(true);
            RefreshOptions();
        }

        private void CycleEquipment(int slotIndex)
        {
            RunState run = CurrentRun();
            if (run == null || slotIndex < 0 || slotIndex > 3)
                return;

            EquipmentStatsCalculator.EnsureSlots(run);
            EquipmentSlotType slot = (EquipmentSlotType)slotIndex;
            var candidates = new List<string>();
            for (int i = 0; i < run.inventoryItemIds.Count; i++)
            {
                string id = run.inventoryItemIds[i];
                if (EquipmentCatalog.Get(id)?.Slot == slot)
                    candidates.Add(id);
            }

            candidates.Sort(StringComparer.Ordinal);
            if (candidates.Count == 0)
            {
                SetText(status, $"교체할 {EquipmentSlotName(slotIndex)} 장비가 없다.");
                return;
            }

            string next = candidates[0];
            string previous = run.equippedItemIds[slotIndex];
            run.inventoryItemIds.Remove(next);
            if (!string.IsNullOrWhiteSpace(previous))
                run.inventoryItemIds.Add(previous);
            run.equippedItemIds[slotIndex] = next;
            EquipmentStatsCalculator.Recalculate(run);
            GameKernel.Services.Get<RunManager>().NotifyStateChanged("equipment.changed");
            SetText(status, $"{EquipmentCatalog.Get(next).DisplayName} 장착");
            Refresh();
        }

        private void EnterBoss()
        {
            RunState run = CurrentRun();
            string bossId = GameKernel.Services.Get<RunProgressionManager>().Campaign.GetAct(run.act).bossId;
            GameKernel.Services.Get<EncounterFlowManager>().TryEnterEncounter(bossId, progressNodeId);
        }

        private void ClaimReward()
        {
            RunState run = CurrentRun();
            RunRewardState reward = run.pendingReward;
            int itemCount = reward?.itemChoiceIds.Count ?? 0;
            string selectedItem = selectedAction >= 0 && selectedAction < itemCount
                ? reward.itemChoiceIds[selectedAction]
                : null;
            int cardIndex = selectedAction - itemCount;
            string selectedCard = reward?.cardChoiceInstanceIds != null &&
                                  cardIndex >= 0 &&
                                  cardIndex < reward.cardChoiceInstanceIds.Count
                ? reward.cardChoiceInstanceIds[cardIndex]
                : null;
            GameKernel.Services.Get<EncounterFlowManager>().ClaimRewardAndContinue(selectedItem, selectedCard);
        }

        private static void EnterFieldOrResult()
        {
            RunState run = CurrentRun();
            if (run != null && run.isComplete)
            {
                GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Result);
                GameKernel.Services.Get<SceneFlowManager>().TryLoad(GameSceneId.Result);
            }
            else
            {
                EnterField();
            }
        }

        private static void EnterField()
        {
            GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Field);
            GameKernel.Services.Get<SceneFlowManager>().TryLoad(GameSceneId.Field);
        }

        private static void ReturnToTitle()
        {
            GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Title);
            GameKernel.Services.Get<SceneFlowManager>().TryLoad(GameSceneId.Title);
        }

        private void CloseToField()
        {
            GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Field);
            Close();
        }

        private void Close()
        {
            if (GameKernel.IsReady)
            {
                if (ScreenId == UIScreenId.Reward)
                {
                    GameKernel.Services.Get<EncounterFlowManager>().ClaimRewardAndContinue();
                    return;
                }

                if (ScreenId == UIScreenId.Shop || ScreenId == UIScreenId.Event || ScreenId == UIScreenId.Rest)
                {
                    GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Field);
                }
                GameKernel.Services.Get<UIManager>().Hide(ScreenId);
            }
        }

        public static RunUIScreenController ShowScreen(
            UIScreenId id,
            string nodeId = "",
            string contentId = "")
        {
            string context = string.IsNullOrWhiteSpace(nodeId)
                ? contentId ?? string.Empty
                : $"{nodeId}::{contentId}";
            return Show(id, context);
        }

        private static RunUIScreenController Show(UIScreenId id, string contextId = "")
        {
            UIScreen shown = GameKernel.Services.Get<UIManager>().Show(id);
            RunUIScreenController controller = shown.GetComponent<RunUIScreenController>();
            controller?.Configure(contextId);
            return controller;
        }

        private void ResolveProgressNode()
        {
            if (!string.IsNullOrWhiteSpace(progressNodeId))
            {
                GameKernel.Services.Get<RunProgressionManager>().ResolveNode(progressNodeId);
            }
        }

        private static void DecodeContext(string context, out string nodeId, out string contentId)
        {
            string value = context ?? string.Empty;
            int separator = value.IndexOf("::", StringComparison.Ordinal);
            if (separator < 0)
            {
                nodeId = string.Empty;
                contentId = value;
                return;
            }

            nodeId = value.Substring(0, separator);
            contentId = value.Substring(separator + 2);
        }

        private static RunState CurrentRun()
        {
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs))
            {
                return null;
            }

            return runs.Current;
        }

        private void SetAction(int index, string label, string detail, bool interactable)
        {
            if (index < 0 || index >= actions.Count)
            {
                return;
            }

            RunScreenActionSlot slot = actions[index];
            SetText(slot.label, label);
            SetText(slot.detail, detail);
            if (slot.button != null)
            {
                slot.button.gameObject.SetActive(!string.IsNullOrWhiteSpace(label));
                slot.button.interactable = interactable;
            }
            if (string.IsNullOrWhiteSpace(label))
                SetActionArtwork(index, null, string.Empty, string.Empty);
        }

        private void SetVisualAction(int index, bool interactable)
        {
            if (index < 0 || index >= actions.Count)
                return;

            RunScreenActionSlot slot = actions[index];
            SetText(slot.label, string.Empty);
            SetText(slot.detail, string.Empty);
            if (slot.button != null)
            {
                slot.button.gameObject.SetActive(true);
                slot.button.interactable = interactable;
            }
        }

        private void SetActionArtwork(int index, Sprite artwork, string title, string detail)
        {
            if (index < 0 || index >= actions.Count)
                return;

            RunScreenActionSlot slot = actions[index];
            if (slot.icon != null)
            {
                slot.icon.sprite = artwork;
                slot.icon.overrideSprite = artwork;
                slot.icon.enabled = artwork != null;
                slot.icon.preserveAspect = true;
            }

            if (slot.button == null)
                return;
            CardBattle.CardHoverSource hover = slot.button.GetComponent<CardBattle.CardHoverSource>();
            if (hover == null)
                hover = slot.button.gameObject.AddComponent<CardBattle.CardHoverSource>();
            if (artwork != null)
                hover.Configure(artwork, title, detail);
            else
                hover.Clear();
        }

        private void WireOptionControls()
        {
            optionTabCallbacks.Clear();
            optionToggleCallbacks.Clear();
            optionSliderCallbacks.Clear();

            for (int i = 0; i < optionTabs.Count; i++)
            {
                int index = i;
                UnityAction callback = () => SetOptionTab(index);
                optionTabCallbacks.Add(callback);
                optionTabs[i]?.onClick.AddListener(callback);
            }

            for (int i = 0; i < optionSlots.Count; i++)
            {
                RunScreenOptionSlot slot = optionSlots[i];
                if (slot == null)
                {
                    optionToggleCallbacks.Add(null);
                    optionSliderCallbacks.Add(null);
                    continue;
                }
                UnityAction<bool> toggleCallback = value => SetOptionToggle(slot.binding, value);
                UnityAction<float> sliderCallback = value => SetOptionSlider(slot.binding, value);
                optionToggleCallbacks.Add(toggleCallback);
                optionSliderCallbacks.Add(sliderCallback);
                slot.toggle?.onValueChanged.AddListener(toggleCallback);
                slot.slider?.onValueChanged.AddListener(sliderCallback);
            }
        }

        private void UnwireOptionControls()
        {
            for (int i = 0; i < optionTabs.Count && i < optionTabCallbacks.Count; i++)
                optionTabs[i]?.onClick.RemoveListener(optionTabCallbacks[i]);
            for (int i = 0; i < optionSlots.Count; i++)
            {
                if (optionSlots[i] == null)
                    continue;
                if (i < optionToggleCallbacks.Count && optionToggleCallbacks[i] != null)
                    optionSlots[i].toggle?.onValueChanged.RemoveListener(optionToggleCallbacks[i]);
                if (i < optionSliderCallbacks.Count && optionSliderCallbacks[i] != null)
                    optionSlots[i].slider?.onValueChanged.RemoveListener(optionSliderCallbacks[i]);
            }

            optionTabCallbacks.Clear();
            optionToggleCallbacks.Clear();
            optionSliderCallbacks.Clear();
        }

        private void RefreshOptionSlot(RunScreenOptionSlot slot, PlayerSettingsData settings)
        {
            if (slot == null)
                return;

            if (slot.toggle != null)
            {
                bool toggleValue = slot.binding switch
                {
                    RunOptionBinding.Fullscreen => settings.fullscreen,
                    RunOptionBinding.ReduceMotion => settings.reduceMotion,
                    RunOptionBinding.ScreenShake => settings.screenShake,
                    RunOptionBinding.HighContrast => settings.highContrastIntents,
                    _ => false
                };
                slot.toggle.SetIsOnWithoutNotify(toggleValue);
                SetText(slot.value, toggleValue ? "ON" : "OFF");
                if (slot.value != null)
                    slot.value.color = toggleValue
                        ? new Color(0.42f, 1f, 0.72f)
                        : new Color(0.72f, 0.76f, 0.84f);
                return;
            }

            if (slot.slider != null)
            {
                float sliderValue = slot.binding switch
                {
                    RunOptionBinding.TextScale => settings.textScale,
                    RunOptionBinding.MasterVolume => settings.masterVolume,
                    RunOptionBinding.MusicVolume => settings.musicVolume,
                    RunOptionBinding.EffectsVolume => settings.effectsVolume,
                    _ => 0f
                };
                slot.slider.SetValueWithoutNotify(sliderValue);
                SetText(slot.value, $"{Mathf.RoundToInt(sliderValue * 100f)}%");
                return;
            }

            if (slot.binding == RunOptionBinding.ControlsInfo)
                SetText(slot.value, "이동: WASD / 방향키   ·   선택: Enter   ·   닫기: Esc");
            else if (slot.binding == RunOptionBinding.DataInfo)
                SetText(slot.value, "설정 자동 저장 ON   ·   런 저장 슬롯 3개");
        }

        private IEnumerator SelectDefaultControlNextFrame()
        {
            yield return null;
            if (EventSystem.current == null)
                yield break;

            Button target = null;
            if (ScreenId == UIScreenId.Options && optionTabs.Count > 0)
                target = optionTabs[Mathf.Clamp(selectedOptionTab, 0, optionTabs.Count - 1)];
            if (target == null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i].button != null && actions[i].button.gameObject.activeInHierarchy &&
                        actions[i].button.interactable)
                    {
                        target = actions[i].button;
                        break;
                    }
                }
            }

            target ??= primaryButton;
            target ??= secondaryButton;
            target ??= closeButton;
            if (target != null && target.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private static void SetText(Text target, string value)
        {
            CardBattle.KeywordTooltipSource.Apply(target, value);
        }

        private static void SetGauge(Slider target, int current, int maximum)
        {
            if (target == null)
            {
                return;
            }

            target.minValue = 0f;
            target.maxValue = Mathf.Max(1, maximum);
            target.value = Mathf.Clamp(current, 0, maximum);
        }

        private static void SetGauge(Image target, int current, int maximum)
        {
            if (target != null)
            {
                target.fillAmount = Mathf.Clamp01(current / (float)Mathf.Max(1, maximum));
            }
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }

        private static string FormatSavedAt(string savedAtUtc)
        {
            return DateTime.TryParse(savedAtUtc, out DateTime savedAt)
                ? savedAt.ToLocalTime().ToString("MM/dd HH:mm")
                : "저장 기록";
        }

        private static string EquipmentSlotName(int index)
        {
            string[] names = { "무기", "의복", "부적", "기념품" };
            return index >= 0 && index < names.Length ? names[index] : "장비";
        }

        private static string EquipmentSlotLabel(EquipmentSlotType slot)
        {
            return slot switch
            {
                EquipmentSlotType.Weapon => "무기",
                EquipmentSlotType.Garment => "의복",
                EquipmentSlotType.Talisman => "부적",
                EquipmentSlotType.Keepsake => "기념품",
                _ => "장비"
            };
        }

        private static string MapCountDetail(RunState run, int index)
        {
            RunFieldContentType type = MapContentType(index);
            int count = run.CurrentActProgress.fieldNodes.FindAll(node => node != null && node.contentType == type && node.discovered).Count;
            return $"발견 {count}";
        }

        private static RunFieldContentType MapContentType(int index)
        {
            return index switch
            {
                0 => RunFieldContentType.Combat,
                1 => RunFieldContentType.Event,
                2 => RunFieldContentType.Shop,
                3 => RunFieldContentType.Supply,
                _ => RunFieldContentType.BossDoor
            };
        }

        private static string MapCategoryName(RunFieldContentType type)
        {
            return type switch
            {
                RunFieldContentType.Combat => "적 건물",
                RunFieldContentType.Event => "사건 건물",
                RunFieldContentType.Shop => "상점",
                RunFieldContentType.Supply => "보급",
                RunFieldContentType.BossDoor => "보스문",
                _ => "목적지"
            };
        }
    }
}
