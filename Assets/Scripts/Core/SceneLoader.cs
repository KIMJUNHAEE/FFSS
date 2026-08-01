using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardBattle
{
    public class SceneLoader : MonoBehaviour
    {
        [Header("씬 이름 (인스펙터에서 실제 씬 이름과 맞춰서 조정)")]
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string mapScene = "Map";
        [SerializeField] private string battleScene = "38_BattleScene";

        public void LoadMainMenu() => Load(mainMenuScene, GameState.MainMenu);
        public void LoadMap() => Load(mapScene, GameState.Map);
        public void LoadBattle() => Load(battleScene, GameState.Battle);
        public void ReloadCurrentScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        private void Load(string sceneName, GameState state)
        {
            GameManager.Instance?.SetState(state);
            SceneManager.LoadScene(sceneName);
        }
    }
}
