using System;
using UnityEngine;

namespace ROLib
{

    public static class ROLConfigNodeUtils
    {
        //input is the string output from ConfigNode.ToString()
        //any other input will result in undefined behavior
        public static ConfigNode ParseConfigNode(string input)
        {
            ConfigNode baseCfn = ConfigNode.Parse(input);
            if (baseCfn == null) { MonoBehaviour.print("ERROR: Base config node was null!!\n" + input); }
            else if (baseCfn.nodes.Count <= 0) { MonoBehaviour.print("ERROR: Base config node has no nodes!!\n" + input); }
            return baseCfn.nodes[0];
        }
    }
}

