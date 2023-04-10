using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Edger.Unity;
using Edger.Unity.Context;

namespace Edger.Unity.Launcher {
    [System.Serializable]
    [CreateAssetMenu(fileName = "launcher", menuName = "Edger/LauncherConfig", order = 1)]
    public class LauncherConfig : ScriptableObject {
        public string HomeScene;
        public CatalogConfig[] MandatoryCatalogs;
        public CatalogConfig[] OptionalCatalogs;

        public bool TryLoadHomeOnError;

        public string DevAssetsUrlFrom;
        public string DevAssetsUrlTo;
    }
}