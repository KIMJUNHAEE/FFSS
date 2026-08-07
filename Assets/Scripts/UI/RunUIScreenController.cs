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

        private readonly List<UnityAction> actionCallbacks = new List<UnityAction>();
        private string sourceId;
        private string progressNodeId;
        private int selectedAction;
        private int page;
        private Coroutine refreshRoutine;
        private IDisposable screenShownSubscription;

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

            refreshRoutine = StartCoroutine(RefreshWhenReady());
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
            screenShownSubscription?.Dispose();
            screenShownSubscription = null;
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
                $"주변 위험 {risk}  ·  전투 {run.CurrentActProgress.normalVictories}/{run.CurrentActProgress.requiredNormalVictories}  ·  사건 {run.CurrentActProgress.completedEvents}/{run.CurrentActProgress.requiredEvents}");
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
                ? "보스문이 열렸다. 지도 끝의 붉은 문양으로 향하자."
                : $"보스문 조건: 전투 {act.normalVictories}/{act.requiredNormalVictories}, 사건 {act.completedEvents}/{act.requiredEvents}, 중간보스 {(act.midBossDefeated ? "완료" : "미완료")}");
            string[] labels = { "전투", "사건", "상점", "보스문" };
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
            bool ready = GameKernel.Services.Get<RunProgressionManager>().CanChallengeBoss(run);
            SetText(heading, $"{act.bossId} 보스문");
            SetText(subtitle, ready ? "입장 가능" : "아직 문이 열리지 않았다");
            SetText(body, ready
                ? "최종 장비와 덱을 확인하자. 문을 넘으면 보스 전투가 시작된다."
                : "필드의 전투·사건·중간보스를 마쳐야 한다.");
            if (primaryButton != null)
            {
                primaryButton.interactable = ready;
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
            SetText(subtitle, $"제{run.act}막 · {FormatTime(run.elapsedSeconds)} · 시드 {run.seed}");
            SetText(body, $"HP {run.player.currentHp}/{run.player.maxHp}\n공격 {run.player.AttackForTurn(2)} · 방어 {run.player.DefenseForTurn(2)} · 압박 {run.player.currentPressure}/{run.player.maxPressure}\n덱 {run.pokerDeck.cards.Count}장 · 장비 {run.equippedItemIds.Count + run.inventoryItemIds.Count}개");
            for (int i = 0; i < actions.Count; i++)
            {
                SetAction(i, $"슬롯 {i + 1}에 저장", i == 0 ? "자동 저장 슬롯" : "수동 저장 슬롯", i < 3);
            }
        }

        private void RefreshOptions()
        {
            PlayerSettingsData settings = PlayerSettingsData.FromPreferences();
            SetText(body, "화면과 전투 연출, 읽기 편의 설정은 즉시 적용되고 저장된다.");
            string[] labels =
            {
                "전체화면",
                "전체 음량",
                "모션 감소",
                "화면 흔들림",
                "의도 고대비",
                "글자 배율"
            };
            string[] values =
            {
                settings.fullscreen ? "켜짐" : "꺼짐",
                $"{Mathf.RoundToInt(settings.masterVolume * 100f)}%",
                settings.reduceMotion ? "켜짐" : "꺼짐",
                settings.screenShake ? "켜짐" : "꺼짐",
                settings.highContrastIntents ? "켜짐" : "꺼짐",
                $"{Mathf.RoundToInt(settings.textScale * 100f)}%"
            };
            for (int i = 0; i < actions.Count; i++)
            {
                SetAction(i, i < labels.Length ? labels[i] : string.Empty, i < values.Length ? values[i] : string.Empty, i < labels.Length);
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
                    ChangeOption(index);
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
            SetText(status, $"슬롯 {slot + 1}에 저장했다.");
        }

        private void Purchase(int index)
        {
            RunShopState shop = GameKernel.Services.Get<RunEconomyManager>().GetOrCreateShop(sourceId);
            if (index < shop.stockIds.Count)
            {
                GameKernel.Services.Get<RunEconomyManager>().TryPurchase(sourceId, shop.stockIds[index]);
                Refresh();
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
                    Show(UIScreenId.Equipment);
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

        private void ChangeOption(int index)
        {
            PlayerSettingsData settings = PlayerSettingsData.FromPreferences();
            switch (index)
            {
                case 0:
                    settings.fullscreen = !settings.fullscreen;
                    break;
                case 1:
                    settings.masterVolume = settings.masterVolume <= 0.26f
                        ? 1f
                        : Mathf.Max(0.25f, settings.masterVolume - 0.25f);
                    break;
                case 2:
                    settings.reduceMotion = !settings.reduceMotion;
                    break;
                case 3:
                    settings.screenShake = !settings.screenShake;
                    break;
                case 4:
                    settings.highContrastIntents = !settings.highContrastIntents;
                    break;
                case 5:
                    settings.textScale = settings.textScale >= 1.49f ? 1f : settings.textScale + 0.25f;
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
                RunFieldContentType.BossDoor => "보스문",
                _ => "목적지"
            };
        }
    }
}
