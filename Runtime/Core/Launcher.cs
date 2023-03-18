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
                StartCoroutine(CalcCatalogsAsync());
            });
            Bus.Target.AddSub(LauncherBus.Msg.SizeCalculated, this, (bus, msg) => {
                StartCoroutine(PreloadCatalogsAsync());
            });
            Bus.Target.AddSub(LauncherBus.Msg.AssetsPreloaded, this, (bus, msg) => {
                StartCoroutine(LoadCatalogsAssembliesAsync());
            });
            Bus.Target.AddSub(LauncherBus.Msg.AssembliesLoaded, this, (bus, msg) => {
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

        private IEnumerator CalcCatalogAsync(CatalogState state) {
            if (state.Status == CatalogStatus.CatalogLoaded) {
                if (string.IsNullOrEmpty(state.Config.PreloadLabel)) {
                    state.Status = CatalogStatus.SizeCalculated;
                } else {
                    state.Status = CatalogStatus.SizeCalculating;
                    var sizeCalculater = Assets.Instance.AssetsSizeCalculator.Target;
                    var op = sizeCalculater.HandleRequestAsync(state.Config.PreloadLabel);
                    while (op.MoveNext()) { yield return op.Current; }

                    var result = sizeCalculater.LastAsync;
                    if (result.IsOk) {
                        var res = result.Response;
                        if (res.Status == AsyncOperationStatus.Succeeded) {
                            state.Status = CatalogStatus.SizeCalculated;
                            state.DownloadSize = res.Result;
                        } else {
                            state.Status = CatalogStatus.SizeCalculateFailed;
                            state.Error = res.Error;
                        }
                    } else {
                        state.Status = CatalogStatus.SizeCalculateFailed;
                        state.Error = result.Error;
                    }
                }
            }
        }

        private IEnumerator CalcCatalogsAsync() {
            Bus.Target.Publish(LauncherBus.Msg.SizeCalculating);
            foreach (var state in CatalogStates.Target.Values) {
                var op = CalcCatalogAsync(state);
                while (op.MoveNext()) { yield return op.Current; }
            }
            bool hasError = false;
            foreach (var state in CatalogStates.Target.Values) {
                if (state.Status != CatalogStatus.SizeCalculated) {
                    Error("CalcCatalog Failed: {0}", state.Config);
                    if (!state.IsOptional) {
                        hasError = true;
                    }
                }
            }
            if (hasError) {
                Bus.Target.Publish(LauncherBus.Msg.SizeCalculateFailed);
            } else {
                Bus.Target.Publish(LauncherBus.Msg.SizeCalculated);
            }
        }

        private IEnumerator PreloadCatalogAsync(CatalogState state) {
            if (state.Status == CatalogStatus.SizeCalculated) {
                if (string.IsNullOrEmpty(state.Config.PreloadLabel)) {
                    state.Status = CatalogStatus.AssetsPreloaded;
                } else {
                    state.Status = CatalogStatus.AssetsPreloading;
                    var assetsPreloader = Assets.Instance.AssetsPreloader.Target;
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

        private IEnumerator LoadCatalogAssemblyAsync(CatalogState state, string assemblyName, bool isOptional) {
            var bytesLoader = Assets.Instance.BytesLoader.Target;
            var op = bytesLoader.HandleRequestAsync(assemblyName);
            while (op.MoveNext()) { yield return op.Current; }

            var result = bytesLoader.LastAsync;
            if (result.IsOk) {
                var res = result.Response;
                if (res.Status == AsyncOperationStatus.Succeeded) {
                    try {
                        AssemblyUtil.LoadAssembly(res.Result);
                        Error("LoadAssembly Succeeded: {0} [{1}]", state.Config.Key, assemblyName);
                    } catch (Exception e) {
                        Error("LoadAssembly Failed: {0} [{1}] -> {2}", state.Config.Key, assemblyName, e);
                        if (!isOptional) {
                            state.Status = CatalogStatus.AssetsPreloadFailed;
                            state.Error = e;
                        }
                    }
                } else if (!isOptional) {
                    state.Status = CatalogStatus.AssetsPreloadFailed;
                    state.Error = res.Error;
                }
            } else if (!isOptional) {
                state.Status = CatalogStatus.AssembliesLoadFailed;
                state.Error = result.Error;
            }
        }


        private IEnumerator LoadCatalogAssembliesAsync(CatalogState state) {
            if (state.Status == CatalogStatus.AssetsPreloaded) {
                int mandatoryCount = state.Config.MandatoryAssemblies == null ? 0 : state.Config.MandatoryAssemblies.Length;
                int optionalCount = state.Config.OptionalAssemblies == null ? 0 : state.Config.OptionalAssemblies.Length;
                if (mandatoryCount == 0 && optionalCount == 0) {
                    state.Status = CatalogStatus.AssembliesLoaded;
                } else {
                    state.Status = CatalogStatus.AssembliesLoading;
                    if (mandatoryCount > 0) {
                        foreach (var assemblyName in state.Config.MandatoryAssemblies) {
                            var op = LoadCatalogAssemblyAsync(state, assemblyName, false);
                            while (op.MoveNext()) { yield return op.Current; }

                            if (state.Status == CatalogStatus.AssembliesLoadFailed) {
                                break;
                            }
                        }
                    }
                    if (state.Status == CatalogStatus.AssembliesLoading && optionalCount > 0) {
                        foreach (var assemblyName in state.Config.OptionalAssemblies) {
                            var op = LoadCatalogAssemblyAsync(state, assemblyName, true);
                            while (op.MoveNext()) { yield return op.Current; }

                            if (state.Status == CatalogStatus.AssembliesLoadFailed) {
                                break;
                            }
                        }
                    }
                    if (state.Status == CatalogStatus.AssembliesLoading) {
                        state.Status = CatalogStatus.AssembliesLoaded;
                    }
                }
            }
        }

        private IEnumerator LoadCatalogsAssembliesAsync() {
            Bus.Target.Publish(LauncherBus.Msg.AssembliesLoading);
            foreach (var state in CatalogStates.Target.Values) {
                var op = LoadCatalogAssembliesAsync(state);
                while (op.MoveNext()) { yield return op.Current; }
            }
            bool hasError = false;
            foreach (var state in CatalogStates.Target.Values) {
                if (state.Status != CatalogStatus.AssembliesLoaded) {
                    Error("LoadCatalogAssembliesAsync Failed: {0}", state.Config);
                    if (!state.IsOptional) {
                        hasError = true;
                    }
                }
            }
            if (hasError) {
                Bus.Target.Publish(LauncherBus.Msg.AssembliesLoadFailed);
            } else {
                Bus.Target.Publish(LauncherBus.Msg.AssembliesLoaded);
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

