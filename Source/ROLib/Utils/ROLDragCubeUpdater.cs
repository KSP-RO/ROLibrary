using System.Collections.Generic;
using UnityEngine;

namespace ROLib
{
    public class ROLDragCubeUpdater
    {
        private bool dragUpdating = false;
        private readonly Part part;

        public ROLDragCubeUpdater(Part part)
        {
            this.part = part;
        }

        private IEnumerator<YieldInstruction> UpdateDragCubesCR(float delay = 0)
        {
            if (dragUpdating) yield break;
            dragUpdating = true;
            if (delay == 0)
                yield return new WaitForFixedUpdate();
            else
                yield return new WaitForSeconds(delay);
            while (HighLogic.LoadedSceneIsFlight && (!FlightGlobals.ready || part.packed || !part.vessel.loaded))
                yield return new WaitForFixedUpdate();
            while (HighLogic.LoadedSceneIsEditor && part.localRoot != EditorLogic.RootPart)
                yield return new WaitForFixedUpdate();
            ROLModInterop.OnPartGeometryUpdate(part, true);
            dragUpdating = false;
        }

        public void Update(float delay = 0)
        {
            if (!dragUpdating)
                part.StartCoroutine(UpdateDragCubesCR(delay));
        }
    }
}
