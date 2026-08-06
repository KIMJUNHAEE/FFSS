using System;
using System.Collections;
using System.Collections.Generic;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Persistence;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
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
        private int selectedAction;
        private int page;
        private Coroutine refreshRoutine;

        public UIScreenId ScreenId => screen != null ? screen.Id : UIScreenId.Title;
        public string SourceId => sourceId;

        public void Configure(string contextId)
        {
            sourceId = contextId ?? string.Empty;
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

            Refresh();
            refreshRoutine = null;
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
            SetText(subtitle, run.regionId);
            SetText(currency, $"{run.gold}냥");
            SetText(status, $"HP {run.player.currentHp}/{run.player.maxHp}  ·  압박 {run.player.currentPressure}/{run.player.maxPressure}  ·  전투 {run.CurrentActProgress.normalVictories}/{run.CurrentActProgress.requiredNormalVictories}  ·  사건 {run.CurrentActProgress.completedEvents}/{run.CurrentActProgress.requiredEvents}");
            SetGauge(hpGauge, run.player.currentHp, run.player.maxHp);
            SetGauge(pressureGauge, run.player.currentPressure, run.player.maxPressure);
        }

        private void RefreshFieldMap()
        {
            RunState run = CurrentRun();
            if (run == null)
            {
                return;
            }

            RunActProgressState act = run.CurrentActProgress;
            SetText(subtitle, $"탐색 {run.visitedNodeIds.Count}/{act.generatedTileCount}  ·  발견 {run.discoveredNodeIds.Count}");
            SetText(body, act.bossDoorUnlocked
                ? "보스문이 열렸다. 지도 끝의 붉은 문양으로 향하자."
                : $"보스문 조건: 전투 {act.normalVictories}/{act.requiredNormalVictories}, 사건 {act.completedEvents}/{act.requiredEvents}, 중간보스 {(act.midBossDefeated ? "완료" : "미완료")}");
            string[] labels = { "전투", "사건", "상점", "휴식", "보스문" };
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
                string id = i < run.equippedItemIds.Count ? run.equippedItemIds[i] : "빈 슬롯";
                SetAction(i, EquipmentSlotName(i), id, i < 4);
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
            for (int i = 0; i < actions.Count; i++)
            {
                if (i >= shop.stockIds.Count)
                {
                    SetAction(i, string.Empty, string.Empty, false);
                    continue;
                }

                RunShopOfferDefinition offer = economy.Catalog.GetOffer(shop.stockIds[i]);
                bool sold = shop.purchasedIds.Contains(offer.offerId);
                SetAction(i, sold ? $"{offer.displayName} · 판매 완료" : $"{offer.displayName} · {offer.price}냥", offer.description, !sold);
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
            for (int i = 0; i < actions.Count; i++)
            {
                if (reward != null && i < reward.itemChoiceIds.Count)
                {
                    SetAction(i, reward.itemChoiceIds[i], "장비 보상", true);
                }
                else if (i < run.pokerDeck.cards.Count)
                {
                    RunCardState card = run.pokerDeck.cards[i];
                    SetAction(i, card.cardId, $"카드 연마 +{card.enhancementLevel + 1}", true);
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
            RunActDefinition act = GameKernel.Services.Get<RunProgressionManager>().Campaign.GetAct(run.act);
            SetText(heading, $"제{run.act}막 돌파");
            SetText(body, $"{act.actRewardGold}냥 · HP {act.transitionHealPercent}% 회복 · 카드 정비 1회\n다음 막의 단서를 확인하고 길을 이어 간다.");
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
            SetText(body, "화면 · 오디오 · 전투 · 접근성 · 조작 · 데이터");
            string[] tabs = { "화면", "오디오", "전투", "접근성", "조작", "데이터" };
            for (int i = 0; i < actions.Count; i++)
            {
                SetAction(i, i < tabs.Length ? tabs[i] : string.Empty, i < tabs.Length ? "설정을 조정한다" : string.Empty, i < tabs.Length);
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
                case UIScreenId.RunStatus:
                    SaveSlot(index);
                    return;
                case UIScreenId.FieldHud:
                    OpenFieldCommand(index);
                    return;
                case UIScreenId.Equipment:
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
                CloseToField();
            }
        }

        private void ChooseRest(int index)
        {
            IReadOnlyList<RunRestOptionDefinition> options = GameKernel.Services.Get<RunEconomyManager>().Catalog.RestOptions;
            if (index < options.Count &&
                GameKernel.Services.Get<RunEconomyManager>().UseRest(sourceId, options[index].type))
            {
                CloseToField();
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

        private void EnterBoss()
        {
            RunState run = CurrentRun();
            string bossId = GameKernel.Services.Get<RunProgressionManager>().Campaign.GetAct(run.act).bossId;
            GameKernel.Services.Get<EncounterFlowManager>().TryEnterEncounter(bossId);
        }

        private void ClaimReward()
        {
            RunState run = CurrentRun();
            string selectedCard = selectedAction < run.pokerDeck.cards.Count
                ? run.pokerDeck.cards[selectedAction].instanceId
                : null;
            GameKernel.Services.Get<RunManager>().ClaimReward(null, selectedCard);
            GameKernel.Services.Get<GameFlowManager>().TryChangeState(GameFlowState.Field);
            EnterField();
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
                GameKernel.Services.Get<UIManager>().Hide(ScreenId);
            }
        }

        private static RunUIScreenController Show(UIScreenId id, string contextId = "")
        {
            UIScreen shown = GameKernel.Services.Get<UIManager>().Show(id);
            RunUIScreenController controller = shown.GetComponent<RunUIScreenController>();
            controller?.Configure(contextId);
            return controller;
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
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
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

        private static string MapCountDetail(RunState run, int index)
        {
            RunFieldContentType type = index switch
            {
                0 => RunFieldContentType.Combat,
                1 => RunFieldContentType.Event,
                2 => RunFieldContentType.Shop,
                3 => RunFieldContentType.Rest,
                _ => RunFieldContentType.BossDoor
            };
            int count = run.CurrentActProgress.fieldNodes.FindAll(node => node != null && node.contentType == type && node.discovered).Count;
            return $"발견 {count}";
        }
    }
}
