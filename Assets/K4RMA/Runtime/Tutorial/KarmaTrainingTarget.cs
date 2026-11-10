using System.Collections.Generic;
using UnityEngine;
namespace KarmaPrototype
{
    public enum KarmaTrainingAttack { Sword, Wave, Rising, Guard }
    public sealed class KarmaTrainingTarget : MonoBehaviour
    {
        public KarmaTrainingAttack Kind;
        Bounds hitBounds;
        public Bounds HitBounds => hitBounds;
        readonly HashSet<int> hits = new HashSet<int>();
        KarmaTutorial tutorial;
        Vector3 origin;
        float shakeUntil;
        public void Initialize(KarmaTutorial owner, KarmaTrainingAttack kind)
        { tutorial = owner; Kind = kind; origin = transform.position; hitBounds = new Bounds(GetComponent<SpriteRenderer>().bounds.center, new Vector3(2.6f, 4, 1)); }
        public bool Hit(KarmaTrainingAttack kind, int execution)
        {
            if (Kind != kind || !tutorial.Accepts(kind) || !hits.Add(execution)) return false;
            shakeUntil = Time.time + 0.22f;
            KarmaAudio.Play(kind == KarmaTrainingAttack.Guard ? KarmaSfxId.CombatGuardBlock : KarmaSfxId.CombatBladeHit);
            KarmaCombatVFX.SpawnSprite(transform.parent, hitBounds.center, "PlayerVFX", "Barrier", Vector2.one * 2, 0.18f);
            tutorial.Success();
            return true;
        }
        void Update()
        {
            if (tutorial.Game.GameplayFrozen) return;
            transform.position = origin + Vector3.right * (Time.time < shakeUntil ? Mathf.Sin(Time.time * 80) * 0.13f : 0);
        }
    }
}
