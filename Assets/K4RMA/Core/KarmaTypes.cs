namespace KarmaPrototype
{
    // Values retained for compatibility with existing serialized settings.
    public enum Essence { Flame = 0, Dash = 1, Ward = 2 }
    public enum RunPhase { Title, Combat, Gate, Choosing, Transition, Victory, Defeat }
    public enum EnemyAttack
    {
        GroundStrike, Bolt, FlameFan, Charge, Ward,
        BlazingCharge, GuardedCharge, EmberAegis, Overdrive
    }
}
