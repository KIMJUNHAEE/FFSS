using System;
using FFSS.Framework.Core;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CardBattle.Exploration
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FieldActAtmosphere : MonoBehaviour
    {
        [Serializable]
        private struct ActLighting
        {
            public Color keyLightColor;
            [Min(0f)] public float keyLightIntensity;
            public Color ambientColor;
        }

        [Header("Preview")]
        [SerializeField, Range(1, 3)] private int previewAct = 1;

        [Header("Scene References")]
        [SerializeField] private Volume globalVolume;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform volumeTrigger;
        [SerializeField] private Light keyLight;

        [Header("Act Profiles")]
        [SerializeField] private VolumeProfile actOneProfile;
        [SerializeField] private VolumeProfile actTwoProfile;
        [SerializeField] private VolumeProfile actThreeProfile;

        [Header("Act Lighting")]
        [SerializeField] private ActLighting actOneLighting = new()
        {
            keyLightColor = new Color(1f, 0.86f, 0.68f, 1f),
            keyLightIntensity = 1.15f,
            ambientColor = new Color(0.19f, 0.17f, 0.18f, 1f)
        };
        [SerializeField] private ActLighting actTwoLighting = new()
        {
            keyLightColor = new Color(0.58f, 0.9f, 0.84f, 1f),
            keyLightIntensity = 1.05f,
            ambientColor = new Color(0.1f, 0.18f, 0.18f, 1f)
        };
        [SerializeField] private ActLighting actThreeLighting = new()
        {
            keyLightColor = new Color(1f, 0.48f, 0.42f, 1f),
            keyLightIntensity = 1.28f,
            ambientColor = new Color(0.18f, 0.08f, 0.11f, 1f)
        };

        private int appliedAct;
        private Camera configuredCamera;
        private Transform configuredTrigger;

        private void OnEnable()
        {
            appliedAct = 0;
            ApplyCurrentAct();
        }

        private void OnValidate()
        {
            previewAct = Mathf.Clamp(previewAct, 1, 3);
            appliedAct = 0;
        }

        private void LateUpdate()
        {
            ApplyCurrentAct();
        }

        private void ApplyCurrentAct()
        {
            ResolveSceneReferences();
            ConfigureCamera();

            int act = ResolveAct();
            if (act == appliedAct)
                return;

            appliedAct = act;
            if (globalVolume != null)
                globalVolume.sharedProfile = ResolveProfile(act);

            ActLighting lighting = ResolveLighting(act);
            if (keyLight != null)
            {
                keyLight.color = lighting.keyLightColor;
                keyLight.intensity = lighting.keyLightIntensity;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = lighting.ambientColor;
        }

        private void ResolveSceneReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (volumeTrigger == null || volumeTrigger == targetCamera?.transform)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    volumeTrigger = player.transform;
                }
                else
                {
                    QuarterViewPlayerController controller =
                        FindFirstObjectByType<QuarterViewPlayerController>();
                    volumeTrigger = controller != null ? controller.transform : targetCamera?.transform;
                }
            }

            if (keyLight == null)
            {
                GameObject lightObject = GameObject.Find("Key Light");
                keyLight = lightObject != null ? lightObject.GetComponent<Light>() : null;
            }
        }

        private void ConfigureCamera()
        {
            if (targetCamera == null ||
                (targetCamera == configuredCamera && volumeTrigger == configuredTrigger))
            {
                return;
            }

            UniversalAdditionalCameraData data = targetCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.stopNaN = true;
            data.dithering = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.volumeTrigger = volumeTrigger != null ? volumeTrigger : targetCamera.transform;

            configuredCamera = targetCamera;
            configuredTrigger = volumeTrigger;
        }

        private int ResolveAct()
        {
            if (!Application.isPlaying || !GameKernel.IsReady ||
                !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
            {
                return previewAct;
            }

            return Mathf.Clamp(runs.Current.act, 1, 3);
        }

        private VolumeProfile ResolveProfile(int act)
        {
            return act switch
            {
                2 => actTwoProfile,
                3 => actThreeProfile,
                _ => actOneProfile
            };
        }

        private ActLighting ResolveLighting(int act)
        {
            return act switch
            {
                2 => actTwoLighting,
                3 => actThreeLighting,
                _ => actOneLighting
            };
        }
    }
}
