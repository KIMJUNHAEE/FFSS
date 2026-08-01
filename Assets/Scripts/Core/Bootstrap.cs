using UnityEngine;

namespace CardBattle
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private SceneLoader sceneLoader;

        private void Start()
        {
            GameManager.Instance?.SetState(GameState.Boot);
            sceneLoader.LoadBattle();
        }
    }
}
