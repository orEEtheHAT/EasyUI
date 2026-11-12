using System;
using EasyBuilding;
using UnityEngine;

namespace EasyUI.Integrations.Building
{
    [AddComponentMenu("EasyUI/Integrations/Building Gameplay Gate")]
    public sealed class BuildingGameplayGate : MonoBehaviour, IWindowGameplayGate
    {
        public BuildingPlacer Placer;
        public IDisposable BlockGameplay(object owner)
        {
            if (!Placer || !Placer.isActiveAndEnabled) throw new InvalidOperationException("Assign an enabled Building Placer to Building Gameplay Gate.");
            return new Lease(Placer, owner);
        }
        private sealed class Lease : IDisposable
        {
            private BuildingPlacer placer; private readonly object owner;
            public Lease(BuildingPlacer placer, object owner) { this.placer = placer; this.owner = owner; placer.SetInputBlocked(owner, true); }
            public void Dispose() { if (placer) placer.SetInputBlocked(owner, false); placer = null; }
        }
    }
}
