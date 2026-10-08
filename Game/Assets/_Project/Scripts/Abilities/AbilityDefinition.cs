using UnityEngine;

namespace K4RMA
{
    [CreateAssetMenu(menuName = "K4RMA/Prototype/Ability Definition", fileName = "AbilityDefinition")]
    public class AbilityDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public bool isPrototype = true;
        [TextArea] public string prototypeNote;
        public bool combatWired;
    }
}
