using System;
using UnityEngine;

namespace ROLib
{
    class ModelWindow : AbstractWindow
    {
        Vector2 variantScroll, coreScroll, noseScroll, mountScroll;
        ModuleROTank module;

        public ModelWindow (ModuleROTank m) :
            base(new Guid(), "ROTanks Model Selection", new Rect(300, 300, 400, 600))
        {
            variantScroll = new Vector2();
            coreScroll = new Vector2();
            noseScroll = new Vector2();
            mountScroll = new Vector2();
            module = m;
        }
    }
}
