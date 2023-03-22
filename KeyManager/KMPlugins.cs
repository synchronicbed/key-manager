﻿using Leosac.KeyManager.Library.Plugin;
using Leosac.KeyManager.Library.Plugin.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Leosac.KeyManager
{
    public class KMPlugins
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        static KMPlugins()
        {
            paths = new List<string>();
            var root = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Plugins");
            paths.AddRange(Directory.GetDirectories(root));
        }

        private static List<string> paths;

        public static void Load()
        {
            log.Info("Loading plugins...");
            paths.ForEach(pluginPath =>
            {
                if (Directory.Exists(pluginPath))
                {
                    var files = Directory.GetFiles(pluginPath, "KeyManager.Library.*.dll").ToList();
                    files.ForEach(file =>
                    {
                        var pluginAssembly = LoadPlugin(file);
                        CreateFactories(pluginAssembly);
                    });
                }
            });
            log.Info("Plugins loaded.");
        }

        private static Assembly LoadPlugin(string pluginPath)
        {
            var loadContext = new PluginLoadContext(pluginPath);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginPath)));
        }

        private static void CreateFactories(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes())
            {
                CreateFactories<KeyStoreFactory>(type);
                CreateFactories<KeyEntryFactory>(type);
                CreateFactories<KeyStoreUIFactory>(type);
                CreateFactories<KeyEntryUIFactory>(type);
                CreateFactories<WizardFactory>(type);
            }
        }

        private static void CreateFactories<T>(Type type) where T : KMFactory<T>
        {
            if (type != null && typeof(T).IsAssignableFrom(type))
            {
                var result = Activator.CreateInstance(type) as T;
                if (result != null)
                {
                    KMFactory<T>.Register(result);
                }
            }
        }
    }
}