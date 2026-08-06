using System.Collections;
using FFSS.Framework.Core;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.UI;
using UnityEngine;

namespace FFSS.Framework.Flow
{
    [DisallowMultipleComponent]
    public sealed class SceneEntryPoint : MonoBehaviour
    {
        [SerializeField] private GameFlowState state;
        [SerializeField] private bool showInitialScreen = true;
        [SerializeField] private UIScreenId initialScreen;
        [SerializeField] private string musicCueId;
        [SerializeField, Min(0f)] private float musicFadeSeconds = 0.8f;

        private IEnumerator Start()
        {
            while (!GameKernel.IsReady)
            {
                yield return null;
            }

            GameServiceRegistry services = GameKernel.Services;
            GameFlowManager flow = services.Get<GameFlowManager>();
            if (flow.Current != state)
            {
                flow.TryChangeState(state);
            }

            if (showInitialScreen)
            {
                services.Get<UIManager>().ShowExclusive(initialScreen, false);
            }

            if (!string.IsNullOrWhiteSpace(musicCueId))
            {
                services.Get<AudioManager>().PlayMusic(musicCueId, musicFadeSeconds);
            }
        }
    }
}
