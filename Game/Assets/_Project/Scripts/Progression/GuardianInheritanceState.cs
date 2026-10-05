using System;
using System.Collections.Generic;

namespace K4RMA
{
    /// <summary>
    /// Immutable view of inherited original techniques. Built from the personalization order.
    /// </summary>
    public sealed class GuardianInheritanceState
    {
        readonly IReadOnlyList<TechniqueId> inherited;

        public GuardianInheritanceState(IReadOnlyList<TechniqueId> inheritedTechniques, GuardianArchetype mainArchetype)
        {
            if (inheritedTechniques == null)
                throw new ArgumentNullException(nameof(inheritedTechniques));

            var copy = new TechniqueId[inheritedTechniques.Count];
            for (int i = 0; i < inheritedTechniques.Count; i++)
                copy[i] = inheritedTechniques[i];
            inherited = Array.AsReadOnly(copy);
            MainArchetype = mainArchetype;
        }

        public GuardianArchetype MainArchetype { get; }
        public IReadOnlyList<TechniqueId> InheritedTechniques => inherited;

        public bool HasTechnique(TechniqueId technique)
        {
            for (int i = 0; i < inherited.Count; i++)
            {
                if (inherited[i] == technique)
                    return true;
            }

            return false;
        }
    }
}
