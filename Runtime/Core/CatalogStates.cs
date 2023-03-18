using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Edger.Unity;
using Edger.Unity.Context;
using Edger.Unity.Addressable;

namespace Edger.Unity.Launcher {
    public enum CatalogStatus {
        CatalogLoading,
        CatalogLoaded,
        CatalogLoadFailed,
        SizeCalculating,
        SizeCalculated,
        SizeCalculateFailed,
        AssetsPreloading,
        AssetsPreloaded,
        AssetsPreloadFailed,
        AssembliesLoading,
        AssembliesLoaded,
        AssembliesLoadFailed,
    }

    public class CatalogState  {
        public bool IsOptional { get; init; }
        public CatalogConfig Config { get; init; }
        public CatalogStatus Status;
        public string LocatorId;
        public object[] AssetsKeys;
        public long DownloadSize;
        public Exception Error;
    }

    public class CatalogStates : DictAspect<string, CatalogState> {
    }
}