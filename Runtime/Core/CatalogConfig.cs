using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Edger.Unity;
using Edger.Unity.Context;

namespace Edger.Unity.Launcher {
    [System.Serializable]
    public class CatalogConfig  {
        public string Key;
        public string Url;
        public string PreloadLabel = "preload";

        public override string ToString() {
            return JsonUtility.ToJson(this);
        }
    }
}