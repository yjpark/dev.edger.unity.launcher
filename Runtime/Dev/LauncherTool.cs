using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.AddressableAssets;

using Edger.Unity;
using Edger.Unity.Dev;
using Edger.Unity.Dev.Remote;

namespace Edger.Unity.Launcher.Dev {
    [DisallowMultipleComponent()]
    public partial class LauncherTool : BaseMono {
        private static LauncherTool _Instance;
        public static LauncherTool Instance {
            get {
                if (_Instance == null) {
                    _Instance = DevTool.Instance.gameObject.GetOrAddComponent<LauncherTool>();
                }
                return _Instance;
            }
        }

        public string AssetsUrlFrom = "";
        public string AssetsUrlTo = "";

        protected override void OnAwake() {
            Addressables.WebRequestOverride = HackWebRequestURL;
        }

        private void HackWebRequestURL(UnityWebRequest request) {
            var url = request.url;
            if (!string.IsNullOrEmpty(AssetsUrlFrom) && !string.IsNullOrEmpty(AssetsUrlTo)) {
                request.url = url.Replace(AssetsUrlFrom, AssetsUrlTo);
                if (url != request.url) {
                    Info("HackWebRequestURL: [Hacked] {0} -> {1}", url, request.url);
                } else {
                    Info("HackWebRequestURL: [Not Changed] {0}", url);
                }
            } else if (LogDebug) {
                Debug("HackWebRequestURL: [Passed] {0}", url);
            }
        }

        private IRemoteSetting _AssetsUrlFrom;
        private IRemoteSetting _AssetsUrlTo;

        public void OnEnable() {
            _AssetsUrlFrom = RemoteTool.Instance.Register("Launcher.AssetsUrlFrom", () => {
                return AssetsUrlFrom;
            }, (value) => {
                AssetsUrlFrom = value;
            });
            _AssetsUrlTo = RemoteTool.Instance.Register("Launcher.AssetsUrlTo", () => {
                return AssetsUrlTo;
            }, (value) => {
                AssetsUrlTo = value;
            });
        }

        public void OnDisable() {
            RemoteTool.Instance.Unregister(ref _AssetsUrlFrom);
            RemoteTool.Instance.Unregister(ref _AssetsUrlTo);
        }
    }
}
