using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    [InitializeOnLoad]
    public sealed class KarmaEndingReview : EditorWindow
    {
        const string Pending = "K4RMA.EndingReview.Pending";
        static readonly string[] Labels = { "A — 계승의 완성 (28초)", "B — 윤회의 끝 (27초)", "C — 마지막 수업 (24초)" };
        int selected;
        static double deadline;
        static KarmaEndingReview()
        {
            EditorApplication.update += ApplyPending;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode) deadline = EditorApplication.timeSinceStartup + 30;
                if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseInt(Pending);
            };
        }
        [MenuItem("K4RMA/리뷰/엔딩 3종")]
        public static void Open() => GetWindow<KarmaEndingReview>("K4RMA 엔딩 비교");
        void OnGUI()
        {
            EditorGUILayout.LabelField("완성된 시련 뒤의 세 가지 방향", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("리뷰용 미리보기입니다. 플레이 중인 진행은 종료됩니다. 일반 캠페인의 기존 엔딩은 그대로 유지됩니다.", MessageType.Info);
            selected = EditorGUILayout.Popup("엔딩", selected, Labels);
            if (GUILayout.Button("선택한 엔딩 시작 / 다시 보기")) Request((KarmaEndingVersion)(selected + 1));
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("현재 엔딩 건너뛰기")) Object.FindAnyObjectByType<KarmaGame>()?.SkipEndingReview();
                if (GUILayout.Button("타이틀로 / 다른 엔딩 선택"))
                {
                    SessionState.EraseInt(Pending); KarmaBossPractice.Stop();
                    Object.FindAnyObjectByType<KarmaGame>()?.ReturnToTitle();
                }
            }
            EditorGUILayout.LabelField("게임 화면에서도 기존 시네마틱 스킵과 타이틀 키를 사용할 수 있습니다. 통계는 실제 클리어 기록으로 표시하지 않습니다.", EditorStyles.wordWrappedLabel);
        }
        internal static void Request(KarmaEndingVersion version)
        {
            if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetInt(Pending, (int)version); deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.isPaused = false;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity"); EditorApplication.EnterPlaymode();
            }
        }
        static void ApplyPending()
        {
            int version = SessionState.GetInt(Pending, 0);
            if (version == 0 || !EditorApplication.isPlaying) return;
            var game = Object.FindAnyObjectByType<KarmaGame>();
            if (game == null)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                { SessionState.EraseInt(Pending); Debug.LogError("Ending review: KarmaGame did not load."); }
                return;
            }
            SessionState.EraseInt(Pending); KarmaBossPractice.Stop();
            game.PreviewEnding((KarmaEndingVersion)version);
        }
    }
}
