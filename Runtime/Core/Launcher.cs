using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

using Edger.Unity;
using Edger.Unity.Context;
using Edger.Unity.Addressable;
using Edger.Unity.Launcher.Dev;

namespace Edger.Unity.Launcher {
    public partial class Launcher : Env, ISingleton {
        private static Launcher _Instance;
        public static Launcher Instance { get => Singleton.GetInstance(ref _Instance); }

        [SerializeField]
        private LauncherConfig _Config;
        public LauncherConfig Config { get => _Config; }

        // Aspects
        public AspectReference<Bus> Bus { get; private set; }

        protected override void OnAwake() {
            Singleton.SetupInstance(ref _Instance, this);

            Bus = CacheAspect<Bus>();

            if (DevMode) {
                LauncherTool.Instance.AssetsUrlFrom = Config.DevAssetsUrlFrom;
                LauncherTool.Instance.AssetsUrlTo = Config.DevAssetsUrlTo;

                Assets.Instance.ContentChannel.Target.DebugMode = true;

                Caching.ClearCache();
            }
        }

        public IEnumerator Start() {
            var catalogLoader = Assets.Instance.CatalogLoader.Target;
            var assetLoader = Assets.Instance.AssetLoader.Target;
            bool failed = false;
            foreach (var catalog in Config.MandatoryCatalogs) {
                var op = catalogLoader.HandleRequestAsync(new CatalogLoader.Req {
                    Key = catalog.Key,
                    Url = catalog.Url,
                });
                while (op.MoveNext()) { yield return op.Current; }
                if (!catalogLoader.LastAsync.IsOk) { failed = true; }
                if (!failed && !string.IsNullOrEmpty(catalog.PreloadLabel)) {
                    op = assetLoader.HandleRequestAsync(new AssetLoader.Req {
                        Key = catalog.PreloadLabel,
                    });
                    while (op.MoveNext()) { yield return op.Current; }
                    if (!assetLoader.LastAsync.IsOk) { failed = true; }
                }
            }
            if (!failed) {
                Addressables.LoadSceneAsync(Config.HomeScene);
            }
        }
    }
}

