using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Edger.Unity;
using Edger.Unity.Addressable;

namespace Edger.Unity.Launcher {
    [System.Serializable]
    public class CatalogConfig  {
        public static string BUILD_TARGET = "[BuildTarget]";
        public static string VERSION = "[Version]";

        public string Key;
        public string Url;
        public string PreloadLabel = "preload";
        public string[] MandatoryAssemblies;
        public string[] OptionalAssemblies;

        public override string ToString() {
            return JsonUtility.ToJson(this);
        }

        public string CalcRealUrl(ILogger logger) {
            var url = Url
                .ReplaceFirst(BUILD_TARGET, AssetsUtil.BuildTarget)
                .ReplaceFirst(VERSION, Application.version);
            logger.Info("CalcRealUrl: {0} -> {1}", Url, url);
            return url;
        }
    }
}