using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        bool openingRequested;
        public float OpeningArrivalFade { get; private set; }
        public string OpeningArrivalSpeech { get; private set; }
        int openingSpeechDismissFrame = -1;
        public void StartOpening()
        {
            openingRequested = true;
            try { StartRun(); } finally { openingRequested = false; }
        }
        void BeginOpening()
        {
            World = new GameObject("노을 수련길").transform;
            World.SetParent(runRoot, false);
            Phase = RunPhase.Tutorial;
            Tutorial = World.gameObject.AddComponent<KarmaTutorial>();
            Tutorial.Initialize(this);
            Player.ResetForRoom(new Vector2(-7, KarmaSpace.PlayerHalfHeight), true);
        }
        internal void FinishOpening()
        {
            if (Tutorial == null || Phase != RunPhase.Tutorial) return;
            Tutorial = null; OpeningArrivalFade = 1;
            OpeningArrivalSpeech = "스승님의 가르침에 따라 수호자들을 쓰러뜨리고, 나만의 기술을 완성해보자.";
            Time.timeScale = 1; Paused = false;
            Elapsed = DamageTaken = 0;
            LoadRoom(true);
        }
        void UpdateOpeningArrivalSpeech()
        {
            if (OpeningArrivalSpeech == null || OpeningArrivalFade >= 0.45f || Paused || SettingsOpen
                || KarmaInput.RebindingThisFrame || !KarmaInput.Pressed(KarmaAction.DialogueConfirm)) return;
            OpeningArrivalSpeech = null;
            openingSpeechDismissFrame = Time.frameCount;
        }
        public void RestartSession()
        {
            openingRequested = false;
            StartRun();
        }
    }
}
