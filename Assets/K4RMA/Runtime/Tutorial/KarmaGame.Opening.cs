using UnityEngine;
namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        bool openingRequested;
        public float OpeningArrivalFade { get; private set; }
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
            Time.timeScale = 1; Paused = false;
            Elapsed = DamageTaken = 0;
            LoadRoom(true);
        }
        public void RestartSession()
        {
            openingRequested = false;
            StartRun();
        }
    }
}
