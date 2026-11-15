using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    // Editor-only audition controls; only the serialized default enters the build.
    [InitializeOnLoad]
    public sealed class KarmaAudioReview : EditorWindow
    {
        const string Pending = "K4RMA.AudioReview.Pending";
        static readonly string[] Labels = { "K4RMA Main Theme", "Four Guardians — Version A",
            "Guardians of K4RMA", "Four Guardians — Version B" };
        int selected = 1;
        static double deadline;

        static KarmaAudioReview()
        {
            EditorApplication.update += ApplyPending;
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode) deadline = EditorApplication.timeSinceStartup + 30;
                if (state == PlayModeStateChange.ExitingPlayMode) SessionState.EraseInt(Pending);
            };
        }
        [MenuItem("K4RMA/리뷰/메인 음악 4곡")]
        static void Open() => GetWindow<KarmaAudioReview>("K4RMA 음악 비교");
        void OnEnable()
        {
            var library = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary");
            if (library != null) selected = library.DefaultMenuTrack;
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("메인 메뉴 음악 비교", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("재생하면 타이틀로 돌아가 기존 트랙을 정지합니다. 루프의 음악적 연결은 청음 확인이 필요합니다.", MessageType.Info);
            selected = EditorGUILayout.Popup("곡", selected, Labels);
            if (GUILayout.Button("선택한 곡 재생 / 처음부터 다시 듣기")) Request(selected);
            if (GUILayout.Button("선택한 곡을 기본 메뉴 음악으로 저장"))
            {
                var library = Resources.Load<KarmaSfxLibrary>("KarmaSfxLibrary");
                if (library != null)
                {
                    Undo.RecordObject(library, "Choose menu theme"); library.DefaultMenuTrack = selected;
                    EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
                }
            }
            if (GUILayout.Button("일반 타이틀로 / 리뷰 종료"))
            {
                SessionState.EraseInt(Pending);
                if (EditorApplication.isPlaying)
                {
                    KarmaBossPractice.Stop(); Object.FindAnyObjectByType<KarmaGame>()?.ReturnToTitle();
                    KarmaAudio.EndMenuReview();
                }
            }
            EditorGUILayout.LabelField("음량은 게임 설정의 음악 / 전체 음량을 사용합니다.", EditorStyles.wordWrappedLabel);
        }
        internal static void Request(int index)
        {
            if (!EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetInt(Pending, Mathf.Clamp(index, 0, 3)); deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.isPaused = false;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/KarmaPrototype.unity"); EditorApplication.EnterPlaymode();
            }
        }
        static void ApplyPending()
        {
            int index = SessionState.GetInt(Pending, -1);
            if (index < 0 || !EditorApplication.isPlaying) return;
            var game = Object.FindAnyObjectByType<KarmaGame>();
            if (game == null)
            {
                if (EditorApplication.timeSinceStartup > deadline)
                { SessionState.EraseInt(Pending); Debug.LogError("Music review: KarmaGame did not load."); }
                return;
            }
            SessionState.EraseInt(Pending);
            KarmaBossPractice.Stop(); game.ReturnToTitle(); KarmaAudio.ReviewMenuTrack(index);
        }
    }
}
