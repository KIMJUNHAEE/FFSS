using UnityEngine;
using UnityEngine.Events;

namespace CardBattle
{
    [System.Serializable] public class CombatantEvent : UnityEvent<CombatantRuntime> { }
    [System.Serializable] public class CardInstanceEvent : UnityEvent<CardInstance> { }

    public class BattleManager : MonoBehaviour
    {
        [Header("참조 (씬/프리팹에서 연결)")]
        public DeckController deck;
        public EnemyData enemyData;

        [Header("플레이어 설정")]
        [SerializeField] private string playerName = "Player";
        [SerializeField] private int playerMaxHp = 70;
        [SerializeField] private int playerMaxEnergy = 3;

        [Header("이벤트 (UI 연동용 - 인스펙터에서 연결)")]
        public UnityEvent OnBattleStart;
        public UnityEvent OnPlayerTurnStart;
        public UnityEvent OnEnemyTurnStart;
        public UnityEvent OnBattleWon;
        public UnityEvent OnBattleLost;
        public CombatantEvent OnPlayerStateChanged;
        public CombatantEvent OnEnemyStateChanged;
        public CardInstanceEvent OnCardPlayed;

        public PlayerRuntime Player { get; private set; }
        public EnemyRuntime Enemy { get; private set; }

        private void Start()
        {
            StartBattle();
        }

        public void StartBattle()
        {
            Player = new PlayerRuntime(playerName, playerMaxHp, playerMaxEnergy);
            Enemy = new EnemyRuntime(enemyData);

            deck.InitializeDeck();

            OnBattleStart?.Invoke();
            StartPlayerTurn();
        }

        public void StartPlayerTurn()
        {
            Player.ResetBlock();
            Player.ResetEnergy();
            deck.DrawHand();

            OnPlayerStateChanged?.Invoke(Player);
            OnPlayerTurnStart?.Invoke();
        }

        public bool TryPlayCard(CardInstance card)
        {
            if (!Player.SpendEnergy(card.data.cost))
                return false;

            CombatantRuntime target = card.data.targetType switch
            {
                CardTargetType.SingleEnemy => Enemy,
                CardTargetType.AllEnemies => Enemy,
                CardTargetType.Self => Player,
                _ => null,
            };

            foreach (var effect in card.data.effects)
                ExecuteEffect(effect, Player, target);

            deck.ResolveCardPlayed(card);

            OnCardPlayed?.Invoke(card);
            OnPlayerStateChanged?.Invoke(Player);
            OnEnemyStateChanged?.Invoke(Enemy);

            CheckBattleEnd();
            return true;
        }

        private void ExecuteEffect(CardEffectData effect, CombatantRuntime source, CombatantRuntime target)
        {
            switch (effect.effectType)
            {
                case CardEffectType.Damage:
                    target?.TakeDamage(effect.value + source.Strength);
                    break;
                case CardEffectType.Block:
                    source.AddBlock(effect.value);
                    break;
                case CardEffectType.Heal:
                    (target ?? source).Heal(effect.value);
                    break;
                case CardEffectType.GainEnergy:
                    if (source is PlayerRuntime player)
                        player.CurrentEnergy += effect.value;
                    break;
                case CardEffectType.ApplyStrength:
                    (target ?? source).Strength += effect.value;
                    break;
                case CardEffectType.Draw:
                    deck.DrawCards(effect.value);
                    break;
            }
        }

        public void EndPlayerTurn()
        {
            deck.DiscardHand();
            StartEnemyTurn();
        }

        private void StartEnemyTurn()
        {
            OnEnemyTurnStart?.Invoke();
            Enemy.ResetBlock();

            var intent = Enemy.CurrentIntent;
            if (intent != null)
            {
                switch (intent.intentType)
                {
                    case EnemyIntentType.Attack:
                        Player.TakeDamage(intent.value + Enemy.Strength);
                        break;
                    case EnemyIntentType.Defend:
                        Enemy.AddBlock(intent.value);
                        break;
                    case EnemyIntentType.Buff:
                        Enemy.Strength += intent.value;
                        break;
                }

                Enemy.AdvanceIntent();
            }

            OnPlayerStateChanged?.Invoke(Player);
            OnEnemyStateChanged?.Invoke(Enemy);

            CheckBattleEnd();

            if (!Player.IsDead && !Enemy.IsDead)
                StartPlayerTurn();
        }

        private void CheckBattleEnd()
        {
            if (Enemy.IsDead) OnBattleWon?.Invoke();
            else if (Player.IsDead) OnBattleLost?.Invoke();
        }
    }
}
