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

        [Header("Scene preview")]
        [SerializeField] private EnemyEncounterDefinition previewEncounter;

        public EnemyEncounterDefinition PreviewEncounter => previewEncounter;
        public bool IsOpen => modalRoot != null && modalRoot.activeSelf;

        private void Awake()
        {
            if (openButton != null)
                openButton.onClick.AddListener(Toggle);
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            Bind(previewEncounter);
            Close();
        }

        private void OnDestroy()
        {
            if (openButton != null)
                openButton.onClick.RemoveListener(Toggle);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void ConfigurePreview(EnemyEncounterDefinition encounter)
        {
            previewEncounter = encounter;
            Bind(encounter);
        }

        public void Bind(EnemyEncounterDefinition encounter)
        {
            if (encounter == null)
                return;

            previewEncounter = encounter;
            EnemyPlayerGuideDefinition guide = encounter.playerGuide ?? new EnemyPlayerGuideDefinition();
            string enemyName = string.IsNullOrWhiteSpace(encounter.displayName) ? encounter.enemyId : encounter.displayName;

            SetText(buttonLabel, "적 정보");
            SetText(titleText, $"{enemyName} 전투 정보");
            SetText(roleText, InlineSection("역할", Fallback(guide.role, RankLabel(encounter.rank))));
            SetText(gimmickText, Section(
                string.IsNullOrWhiteSpace(encounter.ruleMeter?.displayName) ? "고유 기믹" : encounter.ruleMeter.displayName,
                Fallback(guide.gimmick, encounter.ruleMeter?.description)));
            SetText(signatureText, Section("전용패", SignatureDescription(encounter)));
            SetText(counterplayText, Section("대응법", Fallback(guide.counterplay, "적의 예고와 수치를 확인하고 공격·방어·다시뽑기를 선택해.")));
            SetText(termsText, BuildTermText(encounter, guide));
        }

        public void Toggle()
        {
            if (modalRoot != null)
                modalRoot.SetActive(!modalRoot.activeSelf);
        }

        public void Close()
        {
            if (modalRoot != null)
                modalRoot.SetActive(false);
        }

        private static string SignatureDescription(EnemyEncounterDefinition encounter)
        {
            EnemySeotdaSignatureCardDefinition signature = encounter.exclusiveSeotdaCard;
            if (signature == null)
                return "이 적의 전용패 정보가 없어.";

            string effect = string.IsNullOrWhiteSpace(signature.effectText)
                ? "예고된 짝패 조건을 만족하면 적 기술이 강화돼."
                : signature.effectText;
            return $"<color=#F6B4FF><b>{signature.displayName}</b></color>  ·  {signature.month}월패\n{effect}";
        }

        private static string BuildTermText(EnemyEncounterDefinition encounter, EnemyPlayerGuideDefinition guide)
        {
            var terms = new List<string>();
            AddUnique(terms, "전용패");
            AddUnique(terms, "격파");
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
                _ => "전투 문법 하나를 가르치는 땡 일반 적"
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
