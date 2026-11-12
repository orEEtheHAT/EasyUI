using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyUI.Samples
{
    public sealed class DemoGameplayGate : MonoBehaviour, IWindowGameplayGate
    {
        private readonly HashSet<object> owners = new HashSet<object>();
        public bool Blocked => owners.Count != 0;
        public int RequestCount => owners.Count;
        public IDisposable BlockGameplay(object owner) { owners.Add(owner); return new Lease(this, owner); }
        private sealed class Lease : IDisposable
        {
            private DemoGameplayGate gate; private readonly object owner;
            public Lease(DemoGameplayGate gate, object owner) { this.gate = gate; this.owner = owner; }
            public void Dispose() { if (gate) gate.owners.Remove(owner); gate = null; }
        }
    }
}
