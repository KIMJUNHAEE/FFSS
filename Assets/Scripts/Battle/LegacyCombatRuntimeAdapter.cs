using System.Collections;
using FFSS.Framework.Combat;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Presentation.Audio;
using FFSS.Framework.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardBattle
{
    [DisallowMultipleComponent]
    public sealed class LegacyCombatRuntimeAdapter : MonoBehaviour
    {
        [SerializeField] private string battleMusicCue = "bgm.battle";
        [SerializeField, Min(0f)] private float musicFadeSeconds = 0.55f;

        private Coroutine connectRoutine;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            QueueConnection(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (connectRoutine != null)
            {
                StopCoroutine(connectRoutine);
                connectRoutine = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            QueueConnection(scene);
        }

        private void QueueConnection(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            PrepareRunEncounter(scene);
            PrepareRunEquipment(scene);

            if (connectRoutine != null)
                StopCoroutine(connectRoutine);
            connectRoutine = StartCoroutine(ConnectAfterSceneStart(scene));
        }

        private IEnumerator ConnectAfterSceneStart(Scene scene)
        {
            yield return null;
            connectRoutine = null;

            RpsCombatController source = FindCombat(scene);
            if (source == null || !GameKernel.IsReady)
                yield break;

            if (GameKernel.Services.TryGet(out RunManager activeRuns) && activeRuns.HasActiveRun)
                source.ApplyRunPlayerState(activeRuns.Current.player);

            EnemyEncounterDefinition encounter = ResolveRunEncounter();

            LegacyCombatFlowBridge flow = source.GetComponent<LegacyCombatFlowBridge>();
            if (flow == null)
                flow = source.gameObject.AddComponent<LegacyCombatFlowBridge>();
            flow.Configure(source, source.battleResultView);

            LegacyCombatFeedbackBridge feedback = source.GetComponent<LegacyCombatFeedbackBridge>();
            if (feedback == null)
                feedback = source.gameObject.AddComponent<LegacyCombatFeedbackBridge>();
            feedback.Configure(source, encounter);

            LegacyEnemyRulePresentationBridge rules = source.GetComponent<LegacyEnemyRulePresentationBridge>();
            if (rules != null && encounter != null)
                rules.Configure(source, source.pokerHand, encounter, rules.MeterView);

            if (GameKernel.Services.TryGet(out RunManager runs) && runs.HasActiveRun &&
                runs.Current.activeEnemyRule != null &&
                GameKernel.Services.TryGet(out AudioManager audio))
            {
                string cueId = feedback.Encounter != null &&
                               !string.IsNullOrWhiteSpace(feedback.Encounter.musicCueId)
                    ? feedback.Encounter.musicCueId
                    : battleMusicCue;
                audio.PlayMusic(cueId, musicFadeSeconds);
            }
        }

        private static void PrepareRunEquipment(Scene scene)
        {
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun)
                return;

            RpsCombatController source = FindCombat(scene);
            if (source == null)
                return;

            EquipmentLoadout loadout = source.equipmentLoadout;
            if (loadout == null)
                loadout = source.GetComponent<EquipmentLoadout>();
            if (loadout == null)
                loadout = source.gameObject.AddComponent<EquipmentLoadout>();

            loadout.Configure(runs.Current.equippedItemIds, false);
            source.equipmentLoadout = loadout;
        }

        private static void PrepareRunEncounter(Scene scene)
        {
            EnemyEncounterDefinition encounter = ResolveRunEncounter();
            if (encounter == null)
                return;

            FindCombat(scene)?.ConfigureEncounterDefinition(encounter);
        }

        private static EnemyEncounterDefinition ResolveRunEncounter()
        {
            if (!GameKernel.IsReady || !GameKernel.Services.TryGet(out RunManager runs) || !runs.HasActiveRun ||
                runs.Current.activeEnemyRule == null ||
                !GameKernel.Services.TryGet(out EncounterFlowManager flow) || flow.Catalog == null)
            {
                return null;
            }

            string enemyId = runs.Current.activeEnemyRule.enemyId;
            for (int i = 0; i < flow.Catalog.Entries.Count; i++)
            {
                EncounterSceneEntry entry = flow.Catalog.Entries[i];
                if (entry != null && entry.enemyId == enemyId)
                    return entry.encounter;
            }

            return null;
        }

        private static RpsCombatController FindCombat(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                RpsCombatController source = roots[i].GetComponentInChildren<RpsCombatController>(true);
                if (source != null)
                    return source;
            }

            return null;
        }
    }
}
