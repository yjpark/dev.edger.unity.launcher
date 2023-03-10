using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Edger.Unity;
using Edger.Unity.Context;
using Edger.Unity.Addressable;

namespace Edger.Unity.Launcher {
    public class LauncherBus : Bus<LauncherBus.Msg> {
        public enum Msg {
            Init,
            CatalogsLoading,
            CatalogsLoaded,
            CatalogsLoadFailed,
            SizeCalculating,
            SizeCalculated,
            SizeCalculateFailed,
            AssetsPreloading,
            AssetsPreloaded,
            AssetsPreloadFailed,
            HomeLoading,
            HomeLoaded,
            HomeLoadFailed,
        }
    }
}