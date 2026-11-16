using System;
using System.Diagnostics;
namespace DesktopEcosphere {
    sealed class FeedClock {
        double next=double.NaN; int interval=-1;
        internal bool Due(double seconds,int minutes,bool running) {
            if(minutes<=0 || !running || minutes!=interval || double.IsNaN(next)) { interval=minutes; next=seconds+Math.Max(0,minutes)*60; return false; }
            if(seconds<next) return false; next=seconds+minutes*60; return true;
        }
        internal void Reset() { next=double.NaN; }
    }
    sealed class SceneLoadWatch {
        readonly Stopwatch clock=Stopwatch.StartNew();
        internal int Retries { get; private set; }
        internal void Start(bool retry) { if(!retry) Retries=0; clock.Restart(); }
        internal bool Retry(bool error) { if(Retries>=2 || clock.Elapsed.TotalSeconds<(error?5:45)) return false; Retries++; clock.Restart(); return true; }
    }
    static class Interaction {
        internal const string Setup="window.habitatInteractionEnabled=true;for(const type of ['pointermove','pointerenter','pointerdown'])document.addEventListener(type,e=>{if(window.habitatInteractionEnabled===false)e.stopImmediatePropagation()},true);";
        internal static string Feed(int portions) { return "window.habitatFeed("+Math.Max(1,Math.Min(3,portions))+")"; }
        internal static string PointerEnabled(bool enabled) { return "window.habitatInteractionEnabled="+(enabled?"true":"false")+";"+(!enabled?"document.querySelector('#scene')?.dispatchEvent(new PointerEvent('pointerleave'));":""); }
    }
}
