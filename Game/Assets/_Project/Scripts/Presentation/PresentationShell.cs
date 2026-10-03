using UnityEngine;

namespace K4RMA
{
    public class PresentationShell : MonoBehaviour
    {
        [SerializeField] GameObject titleRoot;
        [SerializeField] GameObject endingRoot;
        [SerializeField] RunDirector director;
        [SerializeField] BossController openingBoss;
        [SerializeField] float titleSeconds = 1.7f;

        float titleTime;
        bool titleClosed;
        bool endingShown;

        void Start()
        {
            if (director == null)
                director = FindAnyObjectByType<RunDirector>();
            if (titleRoot != null)
                titleRoot.SetActive(true);
            if (endingRoot != null)
                endingRoot.SetActive(false);
            director?.SetControlsLocked(true);
            if (openingBoss != null)
                openingBoss.enabled = false;
        }

        void Update()
        {
            if (!titleClosed)
            {
                titleTime += Time.unscaledDeltaTime;
                if (titleTime >= titleSeconds)
                    CloseTitle();
            }

            if (!endingShown && director != null && director.State != null && director.State.Phase == RunPhase.SliceComplete)
            {
                endingShown = true;
                if (endingRoot != null)
                    endingRoot.SetActive(true);
            }
        }

        void CloseTitle()
        {
            titleClosed = true;
            if (titleRoot != null)
                titleRoot.SetActive(false);
            director?.SetControlsLocked(false);
            if (openingBoss != null && director != null && director.State != null && director.State.Phase == RunPhase.Fight)
                openingBoss.enabled = true;
        }
    }
}
