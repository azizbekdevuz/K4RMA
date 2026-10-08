namespace K4RMA
{
    public static class RisingSlashRules
    {
        public static bool CanActivate(bool alive, bool grounded, bool slashActive, float cooldownRemaining)
        {
            return alive && grounded && !slashActive && cooldownRemaining <= 0f;
        }
    }
}
