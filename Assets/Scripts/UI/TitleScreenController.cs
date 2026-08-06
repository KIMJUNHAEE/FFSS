using System;
using System.Collections;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Persistence;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.UI;

namespace FFSS.UI
{
    public sealed class TitleScreenController : MonoBehaviour
    {
        private const string MasterVolumeKey = "settings.masterVolume";
        private const string FullscreenKey = "settings.fullscreen";

        [Header("Main menu")]
        [SerializeField] private Button newRunButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;

        [Header("Options")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button closeOptionsButton;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Toggle fullscreenToggle;

        private void OnEnable()
        {
            newRunButton.onClick.AddListener(StartNewRun);
            continueButton.onClick.AddListener(ContinueRun);
            optionsButton.onClick.AddListener(OpenOptions);
            quitButton.onClick.AddListener(Quit);
            closeOptionsButton.onClick.AddListener(CloseOptions);
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            StartCoroutine(RefreshWhenReady());
        }

        private void OnDisable()
        {
            newRunButton.onClick.RemoveListener(StartNewRun);
            continueButton.onClick.RemoveListener(ContinueRun);
            optionsButton.onClick.RemoveListener(OpenOptions);
            quitButton.onClick.RemoveListener(Quit);
            closeOptionsButton.onClick.RemoveListener(CloseOptions);
            masterVolumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
            StopAllCoroutines();
        }

        private IEnumerator RefreshWhenReady()
        {
            while (!GameKernel.IsReady)
            {
                yield return null;
            }

            continueButton.interactable = GameKernel.Services.Get<SaveManager>().HasSave(0);
            float volume = PlayerPrefs.GetFloat(MasterVolumeKey, 0.85f);
            bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            masterVolumeSlider.SetValueWithoutNotify(volume);
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
            AudioListener.volume = volume;
            optionsPanel.SetActive(false);
        }

        private void StartNewRun()
        {
            if (!GameKernel.IsReady)
            {
                return;
            }

            int seed = unchecked(Environment.TickCount ^ DateTime.UtcNow.Millisecond);
            GameKernel.Services.Get<RunManager>().StartNewRun(seed);
            EnterField();
        }

        private void ContinueRun()
        {
            if (!GameKernel.IsReady)
            {
                return;
            }

            SaveGameData loaded = GameKernel.Services.Get<SaveManager>().Load(0);
            if (loaded != null)
            {
                EnterField();
            }
        }

        private static void EnterField()
        {
            GameFlowManager flow = GameKernel.Services.Get<GameFlowManager>();
            if (flow.TryChangeState(GameFlowState.Field))
            {
                GameKernel.Services.Get<SceneFlowManager>().TryLoad(GameSceneId.Field);
            }
        }

        private void OpenOptions()
        {
            optionsPanel.SetActive(true);
        }

        private void CloseOptions()
        {
            optionsPanel.SetActive(false);
            PlayerPrefs.Save();
        }

        private static void SetMasterVolume(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
        }

        private static void SetFullscreen(bool value)
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        private static void Quit()
        {
            Application.Quit();
        }
    }
}
