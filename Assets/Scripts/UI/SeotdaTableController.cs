using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FFSS.Framework.Run;
using Text = TMPro.TMP_Text;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>적 턴에 테이블 위에 섰다 카드 2장을 공개하고 족보를 보여준다.</summary>
    public class SeotdaTableController : MonoBehaviour
    {
        [Header("섰다 카드 스프라이트 20장 (인스펙터에서 채움)")]
        public List<Sprite> deckSprites = new();

        [Header("보스 대표 패")]
        public List<Sprite> signatureSprites = new();
        [Range(0f, 1f)] public float signatureCardChance = 0.72f;
        [Range(0f, 1f)] public float signaturePairChance = 0.18f;

        [Header("참조")]
        public Image cardSlotA;
        public Image cardSlotB;
        public Text rankText;
        public RectTransform drawOrigin;
        public Sprite backSprite;

        [Header("딜 연출")]
        [SerializeField] private float drawDuration = 0.34f;
        [SerializeField] private float drawStagger = 0.16f;
        [SerializeField] private float gatherDuration = 0.22f;
        [SerializeField] private float retractDuration = 0.28f;

        private Coroutine drawRoutine;
        private BossCombatProfile profile;
        private FFSS.Framework.Combat.EnemySeotdaDeckDefinition exclusiveDeckAsset;
        private FFSS.Framework.Combat.EnemySeotdaSignatureCardDefinition signatureCardAsset;
        private OpponentSeotdaCardDefinition signatureDefinition;
        private Sprite signatureSprite;
        private EnemyRuleState ruleState;
        private Sprite preparedFaceSprite;
        private Sprite preparedHiddenSprite;
        private int preparedBasePower;
        private int preparedMinimumBonus = -2;
        private int preparedMaximumBonus = 6;
        public SeotdaHandResult LastResult { get; private set; }
        public SeotdaHandResult PreparedResult { get; private set; }
        public EnemySeotdaHandBand PreparedHandBand => ruleState?.Seotda.preview.handBand ?? EnemySeotdaHandBand.Low;
        public FFSS.Framework.Combat.EnemySeotdaDeckDefinition ExclusiveDeckAsset => exclusiveDeckAsset;
        public FFSS.Framework.Combat.EnemySeotdaSignatureCardDefinition ExclusiveCardAsset => signatureCardAsset;
        public Sprite ExclusiveCardSprite => signatureSprite;
        public Sprite PreparedFaceSprite => preparedFaceSprite;
        public Sprite PreparedHiddenSprite => preparedHiddenSprite;
        public string PreviewSummary { get; private set; } = string.Empty;
        public bool HasPreparedHand => preparedFaceSprite != null && preparedHiddenSprite != null;
        public event Action<RectTransform> CardRevealed;
        public event Action HandRetracted;

        public void BindRuleState(EnemyRuleState state)
        {
            if (state == null)
            {
                return;
            }

            if (ruleState != null && ruleState != state && ruleState.Seotda.faceCard != null && state.Seotda.faceCard == null)
            {
                state.seotda = ruleState.seotda;
            }

            ruleState = state;
            RestorePreparedSprites();
        }

        public string PrepareEnemyHandPreview(int basePower = 0, int minimumBonus = -2, int maximumBonus = 6)
        {
            EnsureRuleState();
            ResetSignatureWindowForPhase();
            RestorePreparedSprites();
            if (!HasPreparedHand)
            {
                SelectPreparedHand();
            }

            PreparedResult = EvaluatePreparedHand();
            UpdatePreview(basePower, minimumBonus, maximumBonus);
            return PreviewSummary;
        }

        public void UpdatePreparedPreview(int basePower, int minimumBonus, int maximumBonus)
        {
            PrepareEnemyHandPreview(basePower, minimumBonus, maximumBonus);
        }

        public void ShowEnemyHandAnimated(Action<SeotdaHandResult> onComplete)
        {
            if (drawRoutine != null) StopCoroutine(drawRoutine);
            drawRoutine = StartCoroutine(ShowEnemyHandRoutine(onComplete));
        }

        public void RetractEnemyHandAnimated(Action onComplete = null)
        {
            if (drawRoutine != null) StopCoroutine(drawRoutine);
            drawRoutine = StartCoroutine(RetractEnemyHandRoutine(onComplete));
        }

        private IEnumerator ShowEnemyHandRoutine(Action<SeotdaHandResult> onComplete)
        {
            PrepareEnemyHandPreview();
            if (!HasPreparedHand)
            {
                LastResult = default;
                drawRoutine = null;
                onComplete?.Invoke(LastResult);
                yield break;
            }

            LastResult = EvaluatePreparedHand();
            if (rankText) rankText.gameObject.SetActive(false);

            yield return DealCard(cardSlotA, preparedFaceSprite, -1f, true);
            yield return new WaitForSeconds(drawStagger);
            yield return DealCard(
                cardSlotB,
                preparedHiddenSprite,
                1f,
                ruleState.Seotda.preview.hiddenCardRevealed);

            if (rankText)
            {
                rankText.text = LastResult.IsValid
                    ? ruleState.Seotda.preview.hiddenCardRevealed
                        ? LastResult.DisplayName
                        : BandLabel(PreparedHandBand)
                    : string.Empty;
                rankText.transform.localScale = Vector3.one * 0.78f;
                rankText.gameObject.SetActive(true);
                yield return ScaleTo(rankText.rectTransform, Vector3.one, 0.16f);
            }

            drawRoutine = null;
            onComplete?.Invoke(LastResult);
        }

        private IEnumerator RetractEnemyHandRoutine(Action onComplete)
        {
            var visibleSlots = new List<Image>();
            if (cardSlotA != null && cardSlotA.gameObject.activeSelf) visibleSlots.Add(cardSlotA);
            if (cardSlotB != null && cardSlotB.gameObject.activeSelf) visibleSlots.Add(cardSlotB);

            if (rankText) rankText.gameObject.SetActive(false);
            if (visibleSlots.Count == 0 || drawOrigin == null)
            {
                ResetAndHide(cardSlotA);
                ResetAndHide(cardSlotB);
                CommitPreparedHand();
                LastResult = default;
                drawRoutine = null;
                HandRetracted?.Invoke();
                onComplete?.Invoke();
                yield break;
            }

            Vector3 gatherWorld = Vector3.zero;
            foreach (var slot in visibleSlots) gatherWorld += slot.rectTransform.position;
            gatherWorld /= visibleSlots.Count;

            int remaining = visibleSlots.Count;
            foreach (var slot in visibleSlots)
                StartCoroutine(MoveSlotStraight(slot, gatherWorld, gatherDuration, 0.96f, () => remaining--));
            yield return new WaitUntil(() => remaining <= 0);
            yield return new WaitForSeconds(0.06f);

            remaining = visibleSlots.Count;
            for (int i = visibleSlots.Count - 1; i >= 0; i--)
            {
                var slot = visibleSlots[i];
                if (backSprite != null) SetCardSprite(slot, backSprite);
                StartCoroutine(MoveSlotStraight(slot, drawOrigin.position, retractDuration, 0.82f, () => remaining--));
            }

            yield return new WaitUntil(() => remaining <= 0);
            foreach (var slot in visibleSlots) ResetAndHide(slot);
            CommitPreparedHand();
            LastResult = default;
            drawRoutine = null;
            HandRetracted?.Invoke();
            onComplete?.Invoke();
        }

        private IEnumerator MoveSlotStraight(Image slot, Vector3 targetWorld, float duration, float targetScale,
            Action onComplete)
        {
            var rt = slot.rectTransform;
            Vector2 from = rt.anchoredPosition;
            Vector2 to = from + WorldDelta(targetWorld, rt);
            float fromAngle = rt.localEulerAngles.z;
            Vector3 fromScale = rt.localScale;

            yield return UiTween.Run(duration, t =>
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromAngle, 0f, t));
                rt.localScale = Vector3.LerpUnclamped(fromScale, Vector3.one * targetScale, t);
            }, UiTween.SmoothStep);

            rt.anchoredPosition = to;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one * targetScale;
            onComplete?.Invoke();
        }

        private IEnumerator DealCard(Image slot, Sprite face, float side, bool revealFace = true)
        {
            if (!slot) yield break;

            var rt = slot.rectTransform;
            Vector2 final = Vector2.zero;
            Vector2 start = drawOrigin != null ? WorldOffset(drawOrigin, rt) : Vector2.down * 170f;
            SetCardSprite(slot, backSprite != null ? backSprite : face);
            slot.gameObject.SetActive(true);
            rt.anchoredPosition = start;
            rt.localRotation = Quaternion.Euler(0f, 0f, side * 13f);
            rt.localScale = Vector3.one * 0.78f;

            bool flipped = false;
            yield return UiTween.Run(drawDuration, t =>
            {
                float eased = UiTween.SmoothStep(t);
                Vector2 arc = Vector2.up * UiTween.SinPunch(t) * 42f;
                rt.anchoredPosition = Vector2.Lerp(start, final, eased) + arc;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(side * 13f, side * -2f, eased));
                float flip = Mathf.Abs(Mathf.Cos(t * Mathf.PI));
                rt.localScale = new Vector3(Mathf.Max(0.06f, flip), Mathf.Lerp(0.78f, 1f, eased), 1f);
                if (!flipped && t >= 0.5f)
                {
                    SetCardSprite(slot, revealFace ? face : backSprite != null ? backSprite : face);
                    flipped = true;
                    if (revealFace)
                    {
                        CardRevealed?.Invoke(rt);
                    }
                }
            });

            SetCardSprite(slot, revealFace ? face : backSprite != null ? backSprite : face);
            rt.anchoredPosition = final;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        private IEnumerator ScaleTo(RectTransform rt, Vector3 target, float duration)
        {
            Vector3 from = rt.localScale;
            yield return UiTween.Run(duration, t => rt.localScale = Vector3.LerpUnclamped(from, target, t),
                UiTween.SmoothStep);
            rt.localScale = target;
        }

        private Vector2 WorldOffset(RectTransform target, RectTransform root)
        {
            return root.anchoredPosition + WorldDelta(target.position, root);
        }

        private Vector2 WorldDelta(Vector3 targetWorld, RectTransform root)
        {
            if (root.parent is not RectTransform parent)
                return Vector2.zero;

            Vector3 targetLocal = parent.InverseTransformPoint(targetWorld);
            Vector3 currentLocal = parent.InverseTransformPoint(root.position);
            Vector3 delta = targetLocal - currentLocal;
            return new Vector2(delta.x, delta.y);
        }

        private static void ResetAndHide(Image slot)
        {
            if (slot == null) return;
            var rt = slot.rectTransform;
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            slot.gameObject.SetActive(false);
        }

        private void SetCardSprite(Image slot, Sprite sprite)
        {
            if (slot == null)
            {
                return;
            }

            slot.sprite = sprite;
            slot.overrideSprite = sprite;
            slot.enabled = true;
            slot.type = Image.Type.Simple;
            slot.preserveAspect = true;
            slot.fillAmount = 1f;
            slot.color = Color.white;

            CardHoverSource hover = slot.GetComponent<CardHoverSource>();
            if (hover == null)
                hover = slot.gameObject.AddComponent<CardHoverSource>();
            if (sprite == null || sprite == backSprite)
                hover.Clear();
            else
                hover.Configure(sprite, SeotdaCardTitle(sprite), SeotdaCardBody(sprite));
        }

        private string SeotdaCardTitle(Sprite sprite)
        {
            if (sprite == signatureSprite && signatureDefinition != null)
                return signatureDefinition.DisplayName;
            if (!SeotdaHandEvaluator.TryParse(sprite, out int month, out bool isGwang))
                return sprite != null ? sprite.name : string.Empty;
            return $"{month}월패{(isGwang ? " · 광" : string.Empty)}";
        }

        private string SeotdaCardBody(Sprite sprite)
        {
            if (sprite == signatureSprite && signatureDefinition != null)
            {
                return $"{profile?.displayName ?? "적"} 전용패\n{signatureDefinition.EffectText}";
            }

            if (!SeotdaHandEvaluator.TryParse(sprite, out int month, out bool isGwang))
                return string.Empty;
            string deckName = exclusiveDeckAsset != null && !string.IsNullOrWhiteSpace(exclusiveDeckAsset.displayName)
                ? exclusiveDeckAsset.displayName
                : "적 전용 섯다 덱";
            return $"{deckName}\n{month}월 {(isGwang ? "광패" : "일반패")}\n두 장이 공개되면 섯다 족보와 기술 추가 효과가 결정돼.";
        }

        public void ConfigureBossProfile(BossCombatProfile combatProfile)
        {
            if (combatProfile == null)
            {
                return;
            }

            profile = combatProfile;
            exclusiveDeckAsset = combatProfile.exclusiveSeotdaDeck;
            if (exclusiveDeckAsset != null && exclusiveDeckAsset.IsConfigured)
            {
                deckSprites = exclusiveDeckAsset.cards
                    .Where(card => card != null && card.faceSprite != null)
                    .Select(card => card.faceSprite)
                    .ToList();
                backSprite = exclusiveDeckAsset.backSprite;
            }
            signatureCardAsset = combatProfile.exclusiveSeotdaCard;
            signatureDefinition = signatureCardAsset != null && signatureCardAsset.IsConfigured
                ? CreateRuntimeDefinition(signatureCardAsset)
                : OpponentSeotdaCardCatalog.Find(combatProfile.bossId);
            signatureSprite = signatureCardAsset != null && signatureCardAsset.faceSprite != null
                ? signatureCardAsset.faceSprite
                : OpponentSeotdaCardCatalog.LoadSprite(signatureDefinition);
            signatureSprites.Clear();
            if (signatureSprite != null) signatureSprites.Add(signatureSprite);
            if (combatProfile.signatureCardA != null) signatureSprites.Add(combatProfile.signatureCardA);
            if (combatProfile.signatureCardB != null && combatProfile.signatureCardB != combatProfile.signatureCardA)
                signatureSprites.Add(combatProfile.signatureCardB);
            signatureCardChance = combatProfile.signatureCardChance;
            signaturePairChance = combatProfile.signaturePairChance;
            EnsureRuleState();
            RestorePreparedSprites();
        }

        private static OpponentSeotdaCardDefinition CreateRuntimeDefinition(
            FFSS.Framework.Combat.EnemySeotdaSignatureCardDefinition asset)
        {
            return new OpponentSeotdaCardDefinition(
                asset.enemyId,
                asset.cardId,
                asset.displayName,
                string.Empty,
                asset.month,
                asset.isGwang,
                (SignatureSeotdaTrigger)(int)asset.trigger,
                asset.triggerMonth,
                asset.tierBonus,
                asset.powerBonus,
                asset.hpDamage,
                asset.breakDamage,
                asset.drawChance,
                asset.effectText);
        }

        public void RevealPreparedHiddenCard()
        {
            EnsureRuleState();
            ruleState.Seotda.preview.hiddenCardRevealed = true;
            UpdatePreview(preparedBasePower, preparedMinimumBonus, preparedMaximumBonus);
            if (cardSlotB != null && cardSlotB.gameObject.activeSelf && preparedHiddenSprite != null)
            {
                SetCardSprite(cardSlotB, preparedHiddenSprite);
                CardRevealed?.Invoke(cardSlotB.rectTransform);
            }
        }

        public void StripPreparedModifier(string modifierId)
        {
            EnsureRuleState();
            ruleState.Seotda.StripModifier(modifierId);
        }

        public void RecordPlayerResponse(bool correct, bool mistake)
        {
            EnsureRuleState();
            ruleState.Seotda.RecordPlayerResponse(correct, mistake);
        }

        public bool ReplacePreparedHiddenWithSafeCard()
        {
            EnsureRuleState();
            if (!HasPreparedHand)
            {
                return false;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Sprite candidate = DrawBaseCard(preparedFaceSprite);
                if (candidate == null)
                {
                    break;
                }

                SeotdaHandResult result = Evaluate(preparedFaceSprite, candidate);
                if (!result.IsValid || result.Tier > 3 || result.IsPair || result.IsGwangPair)
                {
                    ruleState.Seotda.discardOrder.Add(candidate.name);
                    continue;
                }

                if (preparedHiddenSprite != null && preparedHiddenSprite != signatureSprite)
                {
                    ruleState.Seotda.discardOrder.Add(preparedHiddenSprite.name);
                }

                preparedHiddenSprite = candidate;
                ruleState.Seotda.hiddenCard = CreateCardState(candidate);
                PreparedResult = result;
                ruleState.Seotda.preview.handBand = Classify(result);
                ruleState.Seotda.preview.riskBand = Risk(result);
                return true;
            }

            return false;
        }

        private void EnsureRuleState()
        {
            if (ruleState == null)
            {
                ruleState = new EnemyRuleState { enemyId = profile != null ? profile.bossId : name };
            }

            if (string.IsNullOrWhiteSpace(ruleState.enemyId) && profile != null)
            {
                ruleState.enemyId = profile.bossId;
            }

            ruleState.Seotda.EnsureCollections();
        }

        private void ResetSignatureWindowForPhase()
        {
            if (profile == null || profile.encounterRank != EnemyEncounterRank.Boss)
            {
                return;
            }

            int phase = Mathf.Max(1, ruleState.phase);
            if (ruleState.Seotda.signaturePhase == phase)
            {
                return;
            }

            ruleState.Seotda.signaturePhase = phase;
            ruleState.Seotda.signatureClock = 0;
        }

        private void SelectPreparedHand()
        {
            ruleState.Seotda.signatureClock++;
            preparedFaceSprite = DrawBaseCard(null);
            preparedHiddenSprite = DrawBaseCard(preparedFaceSprite);

            AvoidRecentHandRepeat();

            if (preparedFaceSprite == signatureSprite || preparedHiddenSprite == signatureSprite)
            {
                ruleState.Seotda.TryUseSignature(SignatureUseCap(), BattleTurnNumber());
            }

            ruleState.Seotda.faceCard = CreateCardState(preparedFaceSprite);
            ruleState.Seotda.hiddenCard = CreateCardState(preparedHiddenSprite);
            ruleState.Seotda.preview.hiddenCardRevealed = false;
            PreparedResult = EvaluatePreparedHand();
        }

        private bool ShouldUseSignature()
        {
            if (signatureDefinition == null || signatureSprite == null || profile == null ||
                ruleState.Seotda.signatureUseCount >= SignatureUseCap())
            {
                return false;
            }

            int battleTurn = BattleTurnNumber();
            if (profile.encounterRank == EnemyEncounterRank.Normal)
            {
                if (ruleState.Seotda.signatureCheckUsed || battleTurn < 4)
                    return false;

                ruleState.Seotda.signatureCheckUsed = true;
                return RollChance($"signature-normal-{battleTurn}", SignatureAppearanceChance());
            }

            if (profile.encounterRank == EnemyEncounterRank.MidBoss)
            {
                if (ruleState.Seotda.signatureUseCount == 0)
                {
                    if (ruleState.Seotda.signatureCheckUsed || battleTurn < 4)
                        return false;

                    ruleState.Seotda.signatureCheckUsed = true;
                    return RollChance($"signature-midboss-{battleTurn}", SignatureAppearanceChance());
                }

                if (ruleState.Seotda.signatureSecondCheckUsed || battleTurn < 8)
                    return false;

                ruleState.Seotda.signatureSecondCheckUsed = true;
                return HasRepeatedMistakes() &&
                       HasSignatureInterval(battleTurn) &&
                       RollChance($"signature-midboss-second-{battleTurn}", SignatureAppearanceChance() * 0.5f);
            }

            if (IsGwang38())
            {
                return ruleState.phase > 1 &&
                       ruleState.Seotda.signatureClock >= 2 &&
                       HasSignatureInterval(battleTurn) &&
                       RollChance($"signature-gwang38-{battleTurn}", SignatureAppearanceChance());
            }

            if (ruleState.Seotda.signatureUseCount == 0)
            {
                if (ruleState.Seotda.signatureCheckUsed || battleTurn < 4)
                    return false;

                ruleState.Seotda.signatureCheckUsed = true;
                return RollChance($"signature-boss-{battleTurn}", SignatureAppearanceChance());
            }

            return ruleState.phase > 1 &&
                   ruleState.Seotda.signatureClock >= 1 &&
                   HasSignatureInterval(battleTurn) &&
                   RollChance($"signature-boss-repeat-{battleTurn}", SignatureAppearanceChance());
        }

        private int BattleTurnNumber()
        {
            return Mathf.Max(ruleState.turnNumber + 1, 1);
        }

        private bool HasSignatureInterval(int battleTurn)
        {
            return ruleState.Seotda.lastSignatureTurn <= 0 ||
                   battleTurn - ruleState.Seotda.lastSignatureTurn >= 3;
        }

        private int SignatureUseCap()
        {
            if (profile == null)
            {
                return 1;
            }

            return profile.encounterRank switch
            {
                EnemyEncounterRank.Normal => 1,
                EnemyEncounterRank.MidBoss => 2,
                EnemyEncounterRank.Boss when IsGwang38() => 3,
                _ => 2
            };
        }

        private bool ShouldPairSignature()
        {
            if (profile == null)
                return RollChance("signature-pair", signaturePairChance);
            if (profile.encounterRank == EnemyEncounterRank.Boss)
                return true;

            float chance = profile.encounterRank == EnemyEncounterRank.MidBoss
                ? ResponseAdjustedChance(0.6f, 0.25f, 0.8f)
                : ResponseAdjustedChance(0.4f, 0.15f, 0.75f);
            return RollChance("signature-pair", chance);
        }

        private float NormalSignatureAppearanceChance()
        {
            return NormalAct() switch
            {
                1 => ResponseAdjustedChance(0.25f, 0.10f, 0.55f),
                2 => ResponseAdjustedChance(0.35f, 0.20f, 0.65f),
                _ => ResponseAdjustedChance(0.45f, 0.30f, 0.75f)
            };
        }

        private float SignatureAppearanceChance()
        {
            float configured = signatureCardAsset != null
                ? Mathf.Clamp01(signatureCardAsset.drawChance)
                : Mathf.Clamp01(signatureCardChance);
            return profile.encounterRank switch
            {
                EnemyEncounterRank.Normal => Mathf.Min(configured, NormalSignatureAppearanceChance(), 0.35f),
                EnemyEncounterRank.MidBoss => Mathf.Min(configured, 0.4f),
                _ => Mathf.Min(configured, 0.5f)
            };
        }

        private float ResponseAdjustedChance(float neutral, float correct, float repeatedMistake)
        {
            if (HasRepeatedMistakes())
                return repeatedMistake;
            return ruleState.Seotda.consecutiveCorrectResponses > 0 ? correct : neutral;
        }

        private bool HasRepeatedMistakes()
        {
            return ruleState.Seotda.consecutiveMistakes >= 2;
        }

        private int NormalAct()
        {
            string id = ruleState?.enemyId ?? profile?.bossId ?? string.Empty;
            int number = 0;
            for (int i = 0; i < id.Length && char.IsDigit(id[i]); i++)
                number = number * 10 + (id[i] - '0');

            if (number <= 4) return 1;
            return number <= 8 ? 2 : 3;
        }

        private bool IsGwang38()
        {
            string id = ruleState?.enemyId ?? profile?.bossId ?? string.Empty;
            return id == "38" || id.Contains("38광땡");
        }

        private bool RollChance(string channel, float chance)
        {
            if (chance <= 0f)
                return false;
            if (chance >= 1f)
                return true;

            return UnityEngine.Random.value < chance;
        }

        private Sprite DrawNonTriggeringSignaturePartner()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                EnsureShoe();
                for (int i = 0; i < ruleState.Seotda.shoeOrder.Count; i++)
                {
                    Sprite candidate = ResolveSprite(ruleState.Seotda.shoeOrder[i]);
                    if (candidate == null || candidate == signatureSprite ||
                        Evaluate(signatureSprite, candidate).SignatureTriggered)
                    {
                        continue;
                    }

                    ruleState.Seotda.shoeOrder.RemoveAt(i);
                    return candidate;
                }

                RebuildShoe(ruleState.turnNumber + ruleState.Seotda.discardOrder.Count + 701 + pass);
            }

            return null;
        }

        private Sprite ResolveSignaturePartner()
        {
            int requiredMonth = signatureDefinition == null
                ? 0
                : signatureDefinition.Trigger == SignatureSeotdaTrigger.SameMonth
                    ? signatureDefinition.Month
                    : signatureDefinition.TriggerMonth;

            Sprite deckPartner = deckSprites.FirstOrDefault(sprite =>
                sprite != null &&
                SeotdaHandEvaluator.TryParse(sprite, out int month, out _) &&
                month == requiredMonth);
            if (deckPartner != null)
            {
                RemoveFromShoe(deckPartner.name);
                return deckPartner;
            }

            if (requiredMonth <= 0)
            {
                return null;
            }

            Sprite profilePartner = FindProfilePartner(requiredMonth);
            if (profilePartner != null)
            {
                RemoveFromShoe(profilePartner.name);
            }
            return profilePartner;
        }

        private Sprite FindProfilePartner(int requiredMonth)
        {
            if (profile == null)
            {
                return null;
            }

            Sprite[] candidates = { profile.signatureCardA, profile.signatureCardB };
            if (requiredMonth > 0)
            {
                Sprite matching = candidates.FirstOrDefault(sprite =>
                    sprite != null &&
                    SeotdaHandEvaluator.TryParse(sprite, out int month, out _) &&
                    month == requiredMonth);
                if (matching != null)
                {
                    return matching;
                }
            }

            return candidates.FirstOrDefault(sprite => sprite != null);
        }

        private void AvoidRecentHandRepeat()
        {
            for (int attempt = 0; attempt < 8 && HasPreparedHand; attempt++)
            {
                string handId = HandId(preparedFaceSprite, preparedHiddenSprite);
                if (!ruleState.Seotda.WasHandPlayedRecently(handId))
                {
                    return;
                }

                ruleState.Seotda.discardOrder.Add(preparedHiddenSprite.name);
                preparedHiddenSprite = DrawBaseCard(preparedFaceSprite);
            }
        }

        private Sprite DrawBaseCard(Sprite exclude)
        {
            EnsureShoe();
            for (int i = 0; i < ruleState.Seotda.shoeOrder.Count; i++)
            {
                string cardId = ruleState.Seotda.shoeOrder[i];
                Sprite sprite = ResolveSprite(cardId);
                if (sprite == null || sprite == exclude)
                {
                    continue;
                }

                ruleState.Seotda.shoeOrder.RemoveAt(i);
                return sprite;
            }

            RebuildShoe(ruleState.turnNumber + ruleState.Seotda.discardOrder.Count + 1);
            return ruleState.Seotda.shoeOrder.Count > 0 ? DrawBaseCard(exclude) : null;
        }

        private void EnsureShoe()
        {
            ruleState.Seotda.shoeOrder.RemoveAll(cardId => ResolveSprite(cardId) == null);
            if (ruleState.Seotda.shoeOrder.Count == 0)
            {
                RebuildShoe(ruleState.turnNumber + ruleState.Seotda.discardOrder.Count);
            }
        }

        private void RebuildShoe(int salt)
        {
            var ids = deckSprites
                .Where(sprite => sprite != null && sprite != signatureSprite)
                .Select(sprite => sprite.name)
                .Distinct()
                .ToList();

            if (signatureSprite != null && ruleState.Seotda.signatureUseCount < SignatureUseCap())
            {
                var replacement = exclusiveDeckAsset?.cards
                    .FirstOrDefault(card =>
                        card != null &&
                        card.faceSprite != null &&
                        signatureDefinition != null &&
                        card.month == signatureDefinition.Month &&
                        card.isGwang == signatureDefinition.IsGwang)
                    ?? exclusiveDeckAsset?.cards.FirstOrDefault(card =>
                        card != null &&
                        card.faceSprite != null &&
                        signatureDefinition != null &&
                        card.month == signatureDefinition.Month);

                if (replacement?.faceSprite != null)
                {
                    ids.Remove(replacement.faceSprite.name);
                }
                else if (ids.Count >= 20)
                {
                    ids.RemoveAt(ids.Count - 1);
                }

                ids.Add(signatureSprite.name);
            }

            int encounterSeed = ruleState.encounterSeed != 0
                ? ruleState.encounterSeed
                : StableHash(ruleState.enemyId);
            if (profile != null && profile.encounterRank == EnemyEncounterRank.Normal && ids.Count > 8)
            {
                var selectionRandom = new System.Random(encounterSeed ^ StableHash("normal-shoe"));
                for (int i = ids.Count - 1; i > 0; i--)
                {
                    int j = selectionRandom.Next(i + 1);
                    (ids[i], ids[j]) = (ids[j], ids[i]);
                }

                int shoeSize = 6 + ((encounterSeed & 0x7fffffff) % 3);
                ids = ids.Take(shoeSize).ToList();
            }

            var random = new System.Random(encounterSeed ^ salt * 397);
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            ruleState.Seotda.shoeOrder = ids;
            ruleState.Seotda.discardOrder.Clear();
        }

        private void RemoveFromShoe(string cardId)
        {
            EnsureShoe();
            ruleState.Seotda.shoeOrder.Remove(cardId);
        }

        private void RestorePreparedSprites()
        {
            if (ruleState == null)
            {
                return;
            }

            preparedFaceSprite = ResolveSprite(ruleState.Seotda.faceCard?.cardId);
            preparedHiddenSprite = ResolveSprite(ruleState.Seotda.hiddenCard?.cardId);
            if (HasPreparedHand)
            {
                PreparedResult = EvaluatePreparedHand();
            }
        }

        private Sprite ResolveSprite(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return null;
            }

            if (signatureSprite != null && signatureSprite.name == cardId)
            {
                return signatureSprite;
            }

            return deckSprites.Concat(signatureSprites)
                .FirstOrDefault(sprite => sprite != null && sprite.name == cardId);
        }

        private SeotdaCardRuntimeState CreateCardState(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            bool isSignature = sprite == signatureSprite && signatureDefinition != null;
            if (isSignature)
            {
                return new SeotdaCardRuntimeState
                {
                    cardId = sprite.name,
                    month = signatureDefinition.Month,
                    isGwang = signatureDefinition.IsGwang,
                    isSignature = true
                };
            }

            SeotdaHandEvaluator.TryParse(sprite, out int month, out bool isGwang);
            return new SeotdaCardRuntimeState
            {
                cardId = sprite.name,
                month = month,
                isGwang = isGwang,
                isSignature = false
            };
        }

        private SeotdaHandResult EvaluatePreparedHand()
        {
            return Evaluate(preparedFaceSprite, preparedHiddenSprite);
        }

        private SeotdaHandResult Evaluate(Sprite face, Sprite hidden)
        {
            return signatureDefinition != null && signatureSprite != null &&
                   (face == signatureSprite || hidden == signatureSprite)
                ? SeotdaHandEvaluator.EvaluateDetails(face, hidden, signatureDefinition, signatureSprite)
                : SeotdaHandEvaluator.EvaluateDetails(face, hidden);
        }

        private void UpdatePreview(int basePower, int minimumBonus, int maximumBonus)
        {
            preparedBasePower = basePower;
            preparedMinimumBonus = minimumBonus;
            preparedMaximumBonus = maximumBonus;
            EnemySeotdaRuntimeState seotda = ruleState.Seotda;
            EnemySeotdaHandBand handBand = Classify(PreparedResult);
            EnemySeotdaRiskBand riskBand = Risk(PreparedResult);
            int minimum = Mathf.Max(0, basePower + Mathf.Min(minimumBonus, maximumBonus));
            int maximum = Mathf.Max(minimum, basePower + Mathf.Max(minimumBonus, maximumBonus));
            if (PreparedResult.SignatureTriggered && signatureDefinition != null)
            {
                maximum += signatureDefinition.PowerBonus + signatureDefinition.HpDamage;
            }

            SeotdaCardRuntimeState face = seotda.faceCard;
            string faceLabel = face == null
                ? "첫 패 미정"
                : $"{face.month}월{(face.isGwang ? " 광" : string.Empty)}";
            seotda.preview.handBand = handBand;
            seotda.preview.riskBand = riskBand;
            seotda.preview.damageMinimum = minimum;
            seotda.preview.damageMaximum = maximum;
            seotda.preview.faceCardLabel = faceLabel;
            seotda.preview.signaturePossible = preparedFaceSprite == signatureSprite || preparedHiddenSprite == signatureSprite;
            seotda.preview.statusIconId = handBand switch
            {
                EnemySeotdaHandBand.Signature => "signature",
                EnemySeotdaHandBand.Ddaeng => "ddang",
                EnemySeotdaHandBand.Named => "named",
                _ => "low"
            };

            string hidden = seotda.preview.hiddenCardRevealed && seotda.hiddenCard != null
                ? $"{seotda.hiddenCard.month}월{(seotda.hiddenCard.isGwang ? " 광" : string.Empty)}"
                : BandLabel(handBand);
            string signature = seotda.preview.signaturePossible ? " · 전용패 가능" : string.Empty;
            PreviewSummary = $"첫 패 {faceLabel} · 둘째 {hidden} · 예상 {minimum}~{maximum}{signature}";
        }

        private void CommitPreparedHand()
        {
            if (ruleState == null || !HasPreparedHand)
            {
                preparedFaceSprite = null;
                preparedHiddenSprite = null;
                return;
            }

            ruleState.Seotda.RecordHand(HandId(preparedFaceSprite, preparedHiddenSprite));
            if (preparedFaceSprite != null)
            {
                ruleState.Seotda.discardOrder.Add(preparedFaceSprite.name);
            }
            if (preparedHiddenSprite != null)
            {
                ruleState.Seotda.discardOrder.Add(preparedHiddenSprite.name);
            }

            ruleState.Seotda.faceCard = null;
            ruleState.Seotda.hiddenCard = null;
            ruleState.Seotda.preview.hiddenCardRevealed = false;
            preparedFaceSprite = null;
            preparedHiddenSprite = null;
            PreparedResult = default;
            PreviewSummary = string.Empty;
        }

        private static EnemySeotdaHandBand Classify(SeotdaHandResult result)
        {
            if (result.HasSignatureCard)
            {
                return EnemySeotdaHandBand.Signature;
            }
            if (result.IsPair || result.IsGwangPair || result.DisplayName?.Contains("땡") == true)
            {
                return EnemySeotdaHandBand.Ddaeng;
            }
            return result.IsSpecial ? EnemySeotdaHandBand.Named : EnemySeotdaHandBand.Low;
        }

        private static EnemySeotdaRiskBand Risk(SeotdaHandResult result)
        {
            return Classify(result) switch
            {
                EnemySeotdaHandBand.Signature => EnemySeotdaRiskBand.Signature,
                EnemySeotdaHandBand.Ddaeng => EnemySeotdaRiskBand.High,
                EnemySeotdaHandBand.Named => EnemySeotdaRiskBand.Medium,
                _ => EnemySeotdaRiskBand.Low
            };
        }

        private static string BandLabel(EnemySeotdaHandBand band)
        {
            return band switch
            {
                EnemySeotdaHandBand.Signature => "전용패 위험",
                EnemySeotdaHandBand.Ddaeng => "땡·광땡 위험",
                EnemySeotdaHandBand.Named => "이름패·교란",
                _ => "낮은 끗·기본기"
            };
        }

        private static string HandId(Sprite a, Sprite b)
        {
            string first = a != null ? a.name : string.Empty;
            string second = b != null ? b.name : string.Empty;
            return string.CompareOrdinal(first, second) <= 0 ? $"{first}|{second}" : $"{second}|{first}";
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash = (hash ^ source[i]) * 16777619;
                }
                return hash;
            }
        }
    }
}
