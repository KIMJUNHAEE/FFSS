using System.Collections.Generic;
using System.Text;
using FFSS.Framework.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FrameworkEnemyEncounterRank = FFSS.Framework.Combat.EnemyEncounterRank;

namespace CardBattle
{
    public sealed class EnemyCombatGuideView : MonoBehaviour
    {
        [Header("Editable prefab references")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text gimmickText;
        [SerializeField] private TMP_Text signatureText;
        [SerializeField] private TMP_Text counterplayText;
        [SerializeField] private TMP_Text termsText;

        [Header("Paged guide")]
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TMP_Text pageIndicatorText;
        [SerializeField] private TMP_Text pageContentText;

        [Header("Scene preview")]
        [SerializeField] private EnemyEncounterDefinition previewEncounter;
        [SerializeField] private CardSuit previewWeakness = CardSuit.None;

        private readonly List<GuidePage> pages = new();
        private int pageIndex;

        public EnemyEncounterDefinition PreviewEncounter => previewEncounter;
        public CardSuit PreviewWeakness => previewWeakness;
        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;
        public int CurrentPage => pageIndex;
        public int PageCount => pages.Count;

        private readonly struct GuidePage
        {
            public GuidePage(string title, string body)
            {
                Title = title;
                Body = body;
            }

            public string Title { get; }
            public string Body { get; }
        }

        private void Awake()
        {
            pageContentText ??= roleText;
            openButton?.onClick.AddListener(Toggle);
            closeButton?.onClick.AddListener(Close);
            previousPageButton?.onClick.AddListener(PreviousPage);
            nextPageButton?.onClick.AddListener(NextPage);
            Bind(previewEncounter);
            Close();
        }

        private void OnDestroy()
        {
            openButton?.onClick.RemoveListener(Toggle);
            closeButton?.onClick.RemoveListener(Close);
            previousPageButton?.onClick.RemoveListener(PreviousPage);
            nextPageButton?.onClick.RemoveListener(NextPage);
        }

        private void Update()
        {
            if (!IsOpen || Keyboard.current == null)
                return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                PreviousPage();
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                NextPage();
        }

        public void ConfigurePreview(EnemyEncounterDefinition encounter)
        {
            previewEncounter = encounter;
            Bind(encounter);
        }

        public void ConfigureWeakness(CardSuit weakness)
        {
            previewWeakness = weakness;
            Bind(previewEncounter);
        }

        public void Bind(EnemyEncounterDefinition encounter)
        {
            if (encounter == null)
                return;

            previewEncounter = encounter;
            EnemyPlayerGuideDefinition guide = encounter.playerGuide ?? new EnemyPlayerGuideDefinition();
            string enemyName = string.IsNullOrWhiteSpace(encounter.displayName) ? encounter.enemyId : encounter.displayName;

            SetText(buttonLabel, "적 정보");
            pages.Clear();
            pages.Add(new GuidePage(
                $"{enemyName} · 개요",
                InlineSection("역할", Fallback(guide.role, RankLabel(encounter.rank))) +
                WeaknessDescription(previewWeakness)));
            pages.Add(new GuidePage(
                "고유 기믹",
                Section(
                    string.IsNullOrWhiteSpace(encounter.ruleMeter?.displayName) ? "고유 규칙" : encounter.ruleMeter.displayName,
                    Fallback(guide.gimmick, encounter.ruleMeter?.description))));
            pages.Add(new GuidePage("전용패", SignatureDescription(encounter)));
            pages.Add(new GuidePage(
                "대응법과 용어",
                Section("대응법", Fallback(guide.counterplay,
                    "적의 예고 행동과 수치를 확인하고 공격·방어·다시 뽑기를 선택해.")) +
                "\n\n" + BuildTermText(encounter, guide, previewWeakness)));

            pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);
            HideLegacySections();
            RefreshPage();
        }

        public void Toggle()
        {
            if (modalRoot == null)
                return;
            modalRoot.SetActive(!modalRoot.activeSelf);
            if (modalRoot.activeSelf)
            {
                pageIndex = 0;
                RefreshPage();
            }
        }

        public void Close()
        {
            modalRoot?.SetActive(false);
        }

        public void PreviousPage()
        {
            if (pages.Count == 0)
                return;
            pageIndex = (pageIndex - 1 + pages.Count) % pages.Count;
            RefreshPage();
        }

        public void NextPage()
        {
            if (pages.Count == 0)
                return;
            pageIndex = (pageIndex + 1) % pages.Count;
            RefreshPage();
        }

        private void RefreshPage()
        {
            if (pages.Count == 0)
                return;

            GuidePage page = pages[Mathf.Clamp(pageIndex, 0, pages.Count - 1)];
            SetText(titleText, page.Title);
            SetText(pageContentText, page.Body);
            SetText(pageIndicatorText, $"{pageIndex + 1} / {pages.Count}");
            if (previousPageButton != null) previousPageButton.interactable = pages.Count > 1;
            if (nextPageButton != null) nextPageButton.interactable = pages.Count > 1;
        }

        private void HideLegacySections()
        {
            TMP_Text[] oldSections = { gimmickText, signatureText, counterplayText, termsText };
            for (int i = 0; i < oldSections.Length; i++)
            {
                if (oldSections[i] != null && oldSections[i] != pageContentText)
                    oldSections[i].gameObject.SetActive(false);
            }

            if (pageContentText != null)
                pageContentText.gameObject.SetActive(true);
        }

        private static string SignatureDescription(EnemyEncounterDefinition encounter)
        {
            EnemySeotdaSignatureCardDefinition signature = encounter.exclusiveSeotdaCard;
            if (signature == null)
                return "이 적에게 등록된 전용패가 없어.";

            string effect = string.IsNullOrWhiteSpace(signature.effectText)
                ? "예고된 족보 조건을 만족하면 이 기술이 강화돼."
                : signature.effectText;
            return $"<color=#F6B4FF><b>{signature.displayName}</b></color> · {signature.month}월패\n" +
                   $"{SignatureFrequencyDescription(encounter)}\n\n{GameTermGlossary.Decorate(effect)}";
        }

        private static string SignatureFrequencyDescription(EnemyEncounterDefinition encounter)
        {
            if (encounter.rank == FrameworkEnemyEncounterRank.Normal)
            {
                int act = NormalAct(encounter.enemyId);
                string chance = act switch
                {
                    1 => "등장 25% · 대응 10% · 반복 실패 55%",
                    2 => "등장 35% · 대응 20% · 반복 실패 65%",
                    _ => "등장 45% · 대응 30% · 반복 실패 75%"
                };
                return $"4턴부터 한 번 판정 · {chance} · 실패한 턴에는 재시도하지 않아.";
            }

            if (encounter.rank == FrameworkEnemyEncounterRank.MidBoss)
                return "4턴 첫 등장 확정 · 반복 실패 중이면 8턴에 50% 재판정 · 최대 2회.";

            if (encounter.enemyId == "38")
                return "HP 75%·45%·25%에서 페이즈 진입 후 2턴 안에 등장 · 전투당 최대 3회.";

            return "4턴과 HP 페이즈 전환 때 등장 · 최소 3턴 간격 · 전투당 최대 2회.";
        }

        private static int NormalAct(string enemyId)
        {
            int number = 0;
            string value = enemyId ?? string.Empty;
            for (int i = 0; i < value.Length && char.IsDigit(value[i]); i++)
                number = number * 10 + (value[i] - '0');

            if (number <= 4) return 1;
            return number <= 8 ? 2 : 3;
        }

        private static string BuildTermText(
            EnemyEncounterDefinition encounter,
            EnemyPlayerGuideDefinition guide,
            CardSuit weakness)
        {
            var terms = new List<string>();
            AddUnique(terms, "전용패");
            AddUnique(terms, "격파");
            if (weakness != CardSuit.None)
                AddUnique(terms, "약점");
            AddUnique(terms, encounter.ruleMeter?.displayName);
            if (guide.relatedTerms != null)
            {
                for (int i = 0; i < guide.relatedTerms.Count; i++)
                    AddUnique(terms, guide.relatedTerms[i]);
            }

            var builder = new StringBuilder("<color=#FFD96A><b>관련 용어</b></color>\n");
            for (int i = 0; i < terms.Count; i++)
            {
                string term = terms[i];
                if (GameTermGlossary.TryFind(term, out GameTermDefinition definition))
                {
                    builder.Append("<color=").Append(definition.Color).Append("><b>")
                        .Append(definition.Term).Append("</b></color>  ")
                        .Append(definition.Description);
                }
                else if (encounter.ruleMeter != null && term == encounter.ruleMeter.displayName)
                {
                    builder.Append("<color=#FFD35A><b>").Append(term).Append("</b></color>  ")
                        .Append(encounter.ruleMeter.description);
                }
                else
                {
                    builder.Append("<color=#FFD35A><b>").Append(term).Append("</b></color>");
                }

                if (i + 1 < terms.Count)
                    builder.Append('\n');
            }
            return builder.ToString();
        }

        private static string WeaknessDescription(CardSuit weakness)
        {
            return weakness == CardSuit.None
                ? "\n<color=#AEB6C8><b>약점</b></color>  등록된 약점이 없어."
                : $"\n<color=#FFD96A><b>약점</b></color>  {weakness.ToSymbol()} 문양";
        }

        private static void AddUnique(List<string> terms, string term)
        {
            if (!string.IsNullOrWhiteSpace(term) && !terms.Contains(term))
                terms.Add(term);
        }

        private static string RankLabel(FrameworkEnemyEncounterRank rank)
        {
            return rank switch
            {
                FrameworkEnemyEncounterRank.Boss => "막의 선택을 시험하는 광땡 보스",
                FrameworkEnemyEncounterRank.MidBoss => "덱의 대응력을 검사하는 특수 족보 중간보스",
                _ => "전투 문법 한 가지를 가르치는 일반 땡"
            };
        }

        private static string Section(string heading, string body)
        {
            return $"<color=#FFD96A><b>{heading}</b></color>\n{GameTermGlossary.Decorate(body ?? string.Empty)}";
        }

        private static string InlineSection(string heading, string body)
        {
            return $"<color=#FFD96A><b>{heading}</b></color>  {GameTermGlossary.Decorate(body ?? string.Empty)}";
        }

        private static string Fallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }
    }
}
