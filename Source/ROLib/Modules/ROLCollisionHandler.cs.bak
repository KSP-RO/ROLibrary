using System;
using UnityEngine;

namespace ROLib
{
    public class ROLCollisionHandler : PartModule
    {
        public void OnCollisionEnter(Collision collision)
        {
            if (part.State == PartStates.DEAD) { return; }
            bool handle = true;
            int len = collision.contacts.Length;
            for (int i = 0; i < len; i++)
            {
                if (collision.contacts[i].otherCollider == part.collider || collision.contacts[i].thisCollider == part.collider) { handle = false; break; }
            }
            if (handle)
            {
                part.HandleCollision(collision);
            }
        }
    }
}
