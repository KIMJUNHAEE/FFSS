using UnityEngine;
using UnityEngine.Events;

namespace CardBattle
{
    public enum GameState
    {
        Boot,
        MainMenu,
        Map,
        Battle,
        GameOver,
    }

    [System.Serializable] public class GameStateEvent : UnityEvent<GameState> { }

    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("상태 (실행 중 인스펙터에서 확인용)")]
        [SerializeField] private GameState currentState = GameState.Boot;

        [Header("이벤트 (UI/사운드 연동용 - 인스펙터에서 연결)")]
        public GameStateEvent OnStateChanged;

        public GameState CurrentState => currentState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetState(GameState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }
    }
}
