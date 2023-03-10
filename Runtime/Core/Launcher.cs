using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        public AspectReference<LauncherBus> Bus { get; private set; }
        public AspectReference<CatalogStates> CatalogStates { get; private set; }

        protected override void OnAwake() {
            Singleton.SetupInstance(ref _Instance, this);

            Bus = CacheAspect<LauncherBus>();
            CatalogStates = CacheAspect<CatalogStates>();

            Bus.Target.AddSub(LauncherBus.Msg.Init, this, (bus, msg) => {
                CatalogStates.Target.Clear();
                StartCoroutine(LoadCatalogsAsync());
            });
            Bus.Target.AddSub(LauncherBus.Msg.CatalogsLoaded, this, (bus, msg) => {
                StartCoroutine(PreloadCatalogsAsync());
            });

            Bus.Target.AddSub(LauncherBus.Msg.AssetsPreloaded, this, (bus, msg) => {
                StartCoroutine(LoadHomeSceneAsync());
            });

            if (DevMode) {
                LauncherTool.Instance.AssetsUrlFrom = Config.DevAssetsUrlFrom;
                LauncherTool.Instance.AssetsUrlTo = Config.DevAssetsUrlTo;

                Bus.Target.DebugMode = true;
                Assets.Instance.AssetsChannel.Target.DebugMode = true;

                Caching.ClearCache();
            }
        }

        private IEnumerator LoadCatalogAsync(CatalogConfig catalog, bool isOptional) {
            var catalogStates = CatalogStates.Target;
            var catalogLoader = Assets.Instance.CatalogLoader.Target;
            CatalogState state;
            if (catalogStates.TryGetValue(catalog.Key, out state)) {
                Critical("Catalog Conflicted: {0} {1} -> {2}", catalog.Key, catalog, state.Config);
            } else {
                state = new CatalogState {
                    IsOptional = isOptional,
                    Config = catalog,
                    Status = CatalogStatus.CatalogLoading,
                };
                catalogStates[catalog.Key] = state;
            }
            var op = catalogLoader.HandleRequestAsync(new CatalogLoader.Req {
                Key = catalog.Key,
                Url = catalog.CalcRealUrl(this),
            });
            while (op.MoveNext()) { yield return op.Current; }

            var result = catalogLoader.LastAsync;
            if (result.IsOk) {
                var res = result.Response;
                if (res.Status == AsyncOperationStatus.Succeeded) {
                    state.Status = CatalogStatus.CatalogLoaded;
                    state.LocatorId = res.Result.LocatorId;
                    state.AssetsKeys = res.Result.Keys.ToArray();
                } else {
                    state.Status = CatalogStatus.CatalogLoadFailed;
                    state.Error = res.Error;
                }
            } else {
                state.Status = CatalogStatus.CatalogLoadFailed;
                state.Error = result.Error;
            }
        }

        private IEnumerator LoadCatalogsAsync() {
            Bus.Target.Publish(LauncherBus.Msg.CatalogsLoading);
            foreach (var catalog in Config.MandatoryCatalogs) {
                var op = LoadCatalogAsync(catalog, false);
                while (op.MoveNext()) { yield return op.Current; }
            }
            foreach (var catalog in Config.OptionalCatalogs) {
                var op = LoadCatalogAsync(catalog, true);
                while (op.MoveNext()) { yield return op.Current; }
            }
            bool hasError = false;
            foreach (var state in CatalogStates.Target.Values) {
                if (state.Status != CatalogStatus.CatalogLoaded) {
                    Error("LoadCatalog Failed: {0}", state.Config);
                    if (!state.IsOptional) {
                        hasError = true;
                    }
                }
            }
            if (hasError) {
                Bus.Target.Publish(LauncherBus.Msg.CatalogsLoadFailed);
            } else {
                Bus.Target.Publish(LauncherBus.Msg.CatalogsLoaded);
            }
        }

        private IEnumerator PreloadCatalogAsync(CatalogState state) {
            if (state.Status == CatalogStatus.CatalogLoaded) {
                var assetsPreloader = Assets.Instance.AssetsPreloader.Target;
                if (string.IsNullOrEmpty(state.Config.PreloadLabel)) {
                    state.Status = CatalogStatus.AssetsPreloaded;
                } else {
                    var op = assetsPreloader.HandleRequestAsync(state.Config.PreloadLabel);
                    while (op.MoveNext()) { yield return op.Current; }

                    var result = assetsPreloader.LastAsync;
                    if (result.IsOk) {
                        var res = result.Response;
                        if (res.Status == AsyncOperationStatus.Succeeded) {
                            state.Status = CatalogStatus.AssetsPreloaded;
                        } else {
                            state.Status = CatalogStatus.AssetsPreloadFailed;
                            state.Error = res.Error;
                        }
                    } else {
                        state.Status = CatalogStatus.AssetsPreloadFailed;
                        state.Error = result.Error;
                    }
                }
            }
        }

        private IEnumerator PreloadCatalogsAsync() {
            Bus.Target.Publish(LauncherBus.Msg.AssetsPreloading);
            foreach (var state in CatalogStates.Target.Values) {
                var op = PreloadCatalogAsync(state);
                while (op.MoveNext()) { yield return op.Current; }
            }
            bool hasError = false;
            foreach (var state in CatalogStates.Target.Values) {
                if (state.Status != CatalogStatus.AssetsPreloaded) {
                    Error("PreloadCatalog Failed: {0}", state.Config);
                    if (!state.IsOptional) {
                        hasError = true;
                    }
                }
            }
            if (hasError) {
                Bus.Target.Publish(LauncherBus.Msg.AssetsPreloadFailed);
            } else {
                Bus.Target.Publish(LauncherBus.Msg.AssetsPreloaded);
            }
        }

        private IEnumerator LoadHomeSceneAsync() {
            Bus.Target.Publish(LauncherBus.Msg.HomeLoading);
            Addressables.LoadSceneAsync(Config.HomeScene);
            yield return null;
            Bus.Target.Publish(LauncherBus.Msg.HomeLoaded);
        }

        private IEnumerator LoadHomeSceneAsync1() {
            Bus.Target.Publish(LauncherBus.Msg.HomeLoading);
            var sceneLoader = Assets.Instance.SceneLoader.Target;
            var op = sceneLoader.HandleRequestAsync(Config.HomeScene);
            while (op.MoveNext()) { yield return op.Current; }

            bool hasError = false;
            var result = sceneLoader.LastAsync;
            if (result.IsOk) {
                var res = result.Response;
                if (res.Status != AsyncOperationStatus.Succeeded) {
                    hasError = true;
                    Error("LoadHomeScene Failed: {0} -> {1} {2}", Config.HomeScene, res.Status, res.Error);
                }
            } else {
                hasError = true;
                Error("LoadHomeScene Failed: {0} -> {1}", Config.HomeScene, result.Error);
            }
            if (hasError) {
                Bus.Target.Publish(LauncherBus.Msg.HomeLoadFailed);
            } else {
                Bus.Target.Publish(LauncherBus.Msg.HomeLoaded);
            }
        }

        public void Start() {
            Bus.Target.Publish(LauncherBus.Msg.Init);
        }
    }
}

