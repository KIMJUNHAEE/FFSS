using CardBattle.Inventory;
using CardBattle.UI;
using FFSS.Framework.Core;
using FFSS.Framework.Flow;
using FFSS.Framework.Run;
using FFSS.Framework.UI;
using UnityEditor;
using UnityEngine;

namespace CardBattle.Editor
{
    public sealed class RunFlowDebugWindow : EditorWindow
    {
        private const int DebugSeed = 381318;
        private string lastAction = "플레이 모드에서 원하는 구간으로 바로 이동할 수 있어.";

        [MenuItem("FFSS/Debug/Run Flow Navigator")]
        private static void Open()
        {
            GetWindow<RunFlowDebugWindow>("Run Flow Debug");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FFSS 런 흐름 디버그", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "게임용 UI를 생성하지 않고, 현재 런을 원하는 검수 구간으로 이동시키는 에디터 도구야.",
                MessageType.Info);

            bool ready = EditorApplication.isPlaying && GameKernel.IsReady;
            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button("새 테스트 런 시작"))
                    StartDebugRun();

                EditorGUILayout.Space(6f);
                DrawActRow(1);
                if (GUILayout.Button("13광땡 이후 휴식 · 1막 → 2막"))
                    ShowIntermission(1);

                DrawActRow(2);
                if (GUILayout.Button("18광땡 이후 휴식 · 2막 → 3막"))
                    ShowIntermission(2);

                DrawActRow(3);

                EditorGUILayout.Space(6f);
                if (GUILayout.Button("현재 막 상점 · 500냥"))
                    OpenShop();
                if (GUILayout.Button("원본 드래그 장비 화면"))
                    InventoryScreenController.Open();
            }

            if (!ready)
                EditorGUILayout.HelpBox("플레이 모드에서 GameKernel 초기화 후 사용할 수 있어.", MessageType.Warning);
            EditorGUILayout.HelpBox(lastAction, MessageType.None);
        }

        private void DrawActRow(int act)
        {
            if (GUILayout.Button($"제{act}막 필드 처음부터"))
                JumpToField(act);
        }

        private void StartDebugRun()
        {
            RunManager runs = GameKernel.Services.Get<RunManager>();
            runs.StartNewRun(DebugSeed);
            JumpToField(1);
        }

        private void JumpToField(int act)
        {
            RunState run = PrepareRunAtAct(act);
            ResetActProgress(run);
            GameKernel.Services.Get<UIManager>().HideAll(false);
            GameKernel.Services.Get<GameFlowManager>().SynchronizeSceneState(GameFlowState.Field);
            GameKernel.Services.Get<SceneFlowManager>().TryLoad(GameSceneId.Field);
            lastAction = $"제{act}막 필드를 초기 상태로 불러왔어.";
            Repaint();
        }

        private void ShowIntermission(int act)
        {
            RunState run = PrepareRunAtAct(act);
            RunActProgressState progress = run.CurrentActProgress;
            progress.bossDefeated = true;
            string restId = RunProgressionManager.IntermissionRestId(act);
            run.consumedRestIds.Remove(restId);
            run.choiceHistory.RemoveAll(choice => choice != null && choice.sourceId == restId);

            UIManager ui = GameKernel.Services.Get<UIManager>();
            ui.HideAll(false);
            GameKernel.Services.Get<GameFlowManager>().SynchronizeSceneState(GameFlowState.ActTransition);
            RunUIScreenController.ShowScreen(UIScreenId.ActTransition);
            lastAction = $"제{act}막 보스 직후 휴식 화면을 열었어.";
            Repaint();
        }

        private void OpenShop()
        {
            RunManager runs = GameKernel.Services.Get<RunManager>();
            RunState run = runs.HasActiveRun ? runs.Current : runs.StartNewRun(DebugSeed);
            run.gold = Mathf.Max(run.gold, 500);
            GameKernel.Services.Get<GameFlowManager>().SynchronizeSceneState(GameFlowState.Event);
            RunUIScreenController.ShowScreen(
                UIScreenId.Shop,
                contentId: $"shop.act{Mathf.Clamp(run.act, 1, 3)}.debug");
            lastAction = $"제{run.act}막 상점을 500냥으로 열었어.";
            Repaint();
        }

        private static RunState PrepareRunAtAct(int act)
        {
            RunManager runs = GameKernel.Services.Get<RunManager>();
            RunState run = runs.HasActiveRun ? runs.Current : runs.StartNewRun(DebugSeed);
            RunCampaignDefinition campaign = GameKernel.Services.Get<RunProgressionManager>().Campaign;
            RunActDefinition definition = campaign.GetAct(act);

            run.act = act;
            run.regionId = definition.regionId;
            run.isComplete = false;
            run.outcome = RunOutcome.InProgress;
            run.result.outcome = RunOutcome.InProgress;
            run.activeEnemyRule = null;
            run.activeEncounterNodeId = string.Empty;
            run.activeCombat = null;
            run.pendingReward = null;
            return run;
        }

        private static void ResetActProgress(RunState run)
        {
            int act = run.act;
            RunActProgressState progress = run.CurrentActProgress;
            progress.normalVictories = 0;
            progress.completedEvents = 0;
            progress.shopVisits = 0;
            progress.restVisits = 0;
            progress.midBossDefeated = false;
            progress.bossDoorUnlocked = false;
            progress.bossDefeated = false;
            progress.hasCurrentCell = false;
            progress.currentAxialX = 0;
            progress.currentAxialY = 0;
            progress.visitedTileIds.Clear();
            progress.fieldNodes.Clear();

            string prefix = $"act{act}.";
            run.completedEncounterIds.RemoveAll(id => id != null && id.StartsWith(prefix));
            run.completedEventIds.RemoveAll(id => id != null && id.StartsWith(prefix));
            run.discoveredNodeIds.RemoveAll(id => id != null && id.StartsWith(prefix));
            run.visitedNodeIds.RemoveAll(id => id != null && id.StartsWith(prefix));
            run.shops.RemoveAll(shop => shop != null && shop.act == act);
        }
    }
}
