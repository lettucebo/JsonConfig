//
// Copyright (C) 2012 Timo Dörr
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace JsonConfig.Core
{
    /// <summary>
    /// JsonConfig Class.
    /// </summary>
    public static class JsonConfig
    {
        public static dynamic Default = new JsonConfigObject();
        public static dynamic User;

        /// <summary>
        /// Gets the merged configuration.
        /// </summary>
        /// <value>The merged configuration.</value>
        public static dynamic MergedConfig
        {
            get
            {
                return Merger.Merge(User, Default);
            }
        }

        public static string DefaultEnding = ".json";

        private static dynamic globalConfig;

        /// <summary>
        /// Gets or sets the global.
        /// </summary>
        /// <value>The global.</value>
        public static dynamic Global
        {
            get
            {
                if (globalConfig == null)
                {
                    globalConfig = MergedConfig;
                }
                return globalConfig;
            }
            set
            {
                globalConfig = Merger.Merge(value, MergedConfig);
            }
        }

        /// <summary>
        /// Gets a CiConfigObject that represents the current configuration. Since it is 
        /// a cloned copy, changes to the underlying configuration files that are done
        /// after GetCurrentScope() is called, are not applied in the returned instance.
        /// </summary>
        public static JsonConfigObject GetCurrentScope()
        {
            if (Global is null)
                return new JsonConfigObject();
            else
                return Global.Clone();
        }

        /// <summary>
        /// Delegate UserConfigFileChangedHandler
        /// </summary>
        public delegate void UserConfigFileChangedHandler();

        /// <summary>
        /// Occurs when [on user configuration file changed].
        /// </summary>
        public static event UserConfigFileChangedHandler OnUserConfigFileChanged;

        /// <summary>
        /// Initializes static members of the <see cref="JsonConfig"/> class.
        /// </summary>
        static JsonConfig()
        {
            // static C'tor, run once to check for compiled/embedded config

            // scan ALL linked assemblies and merge their default configs while
            // giving the entry assembly top priority in merge
            var entryAssembly = Assembly.GetEntryAssembly();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies.Where(assembly => !assembly.Equals(entryAssembly)))
            {
                Default = Merger.Merge(GetDefaultConfig(assembly), Default);
            }
            if (entryAssembly != null)
                Default = Merger.Merge(GetDefaultConfig(entryAssembly), Default);

            // User config (provided through a settings.conf file)
            var executionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "JsonConfig");

            // Auto find all json file in the folder
            var d = new DirectoryInfo(executionPath);
            var userConfig = (from FileInfo fi in d.GetFiles()
                              where (
                                  fi.FullName.EndsWith(".json")
                              )
                              select fi);

            // Merge multiple config file
            if (userConfig != null)
            {
                if (userConfig.Count() > 1)
                {
                    List<JsonConfigObject> configObjects = new List<JsonConfigObject>();
                    foreach (var item in userConfig)
                    {
                        configObjects.Add(JsonConfig.ParseJson(File.ReadAllText(item.FullName)));
                    }
                    User = Merger.MergeMultiple(configObjects.ToArray());
                }
                else
                {
                    User = JsonConfig.ParseJson(File.ReadAllText(userConfig.FirstOrDefault().FullName));
                    WatchUserConfig(userConfig.FirstOrDefault());
                }
            }
            else
            {
                User = null;
            }
        }

        /// <summary>
        /// The user configuration file watcher
        /// </summary>
        private static FileSystemWatcher userConfigWatcher;

        /// <summary>
        /// Watches the user configuration file.
        /// </summary>
        /// <param name="info">The information.</param>
        public static void WatchUserConfig(FileInfo info)
        {
            var lastRead = File.GetLastWriteTime(info.FullName);
            userConfigWatcher = new FileSystemWatcher(info.Directory.FullName, info.Name);
            userConfigWatcher.NotifyFilter = NotifyFilters.LastWrite;
            userConfigWatcher.Changed += delegate
            {
                DateTime lastWriteTime = File.GetLastWriteTime(info.FullName);
                if (lastWriteTime.Subtract(lastRead).TotalMilliseconds > 100)
                {
                    Console.WriteLine("User configuration has changed, updating config information");
                    try
                    {
                        User = (JsonConfigObject)ParseJson(File.ReadAllText(info.FullName));
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(100); //Sleep shortly, and try again.
                        try
                        {
                            User = (JsonConfigObject)ParseJson(File.ReadAllText(info.FullName));
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Updating user config failed.");
                            throw;
                        }
                    }

                    // invalidate the Global config, forcing a re-merge next time its accessed
                    globalConfig = null;

                    // trigger our event
                    if (OnUserConfigFileChanged != null)
                        OnUserConfigFileChanged();
                }
                lastRead = lastWriteTime;
            };
            userConfigWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Applies the json from file information.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>CiConfigObject.</returns>
        public static JsonConfigObject ApplyJsonFromFileInfo(FileInfo file, JsonConfigObject config = null)
        {
            var overlayJson = File.ReadAllText(file.FullName);
            dynamic overlayConfig = ParseJson(overlayJson);
            return Merger.Merge(overlayConfig, config);
        }

        /// <summary>
        /// Applies the json from path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>CiConfigObject.</returns>
        public static JsonConfigObject ApplyJsonFromPath(string path, JsonConfigObject config = null)
        {
            return ApplyJsonFromFileInfo(new FileInfo(path), config);
        }

        /// <summary>
        /// Applies the json.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>CiConfigObject.</returns>
        public static JsonConfigObject ApplyJson(string json, JsonConfigObject config = null)
        {
            if (config == null)
                config = new JsonConfigObject();

            dynamic parsed = ParseJson(json);
            return Merger.Merge(parsed, config);
        }

        /// <summary>
        /// seeks a folder for .conf files
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="recursive">if set to <c>true</c> [recursive].</param>
        /// <returns>CiConfigObject.</returns>
        public static JsonConfigObject ApplyFromDirectory(string path, JsonConfigObject config = null, bool recursive = false)
        {
            if (!Directory.Exists(path))
                throw new Exception("no folder found in the given path");

            if (config == null)
                config = new JsonConfigObject();

            DirectoryInfo info = new DirectoryInfo(path);
            if (recursive)
            {
                foreach (var dir in info.GetDirectories())
                {
                    Console.WriteLine("reading in folder {0}", dir.ToString());
                    config = ApplyFromDirectoryInfo(dir, config, recursive);
                }
            }

            // find all files
            var files = info.GetFiles();
            foreach (var file in files)
            {
                Console.WriteLine("reading in file {0}", file.ToString());
                config = ApplyJsonFromFileInfo(file, config);
            }
            return config;
        }

        /// <summary>
        /// Applies from directory information.
        /// </summary>
        /// <param name="info">The information.</param>
        /// <param name="config">The configuration.</param>
        /// <param name="recursive">if set to <c>true</c> [recursive].</param>
        /// <returns>CiConfigObject.</returns>
        public static JsonConfigObject ApplyFromDirectoryInfo(DirectoryInfo info, JsonConfigObject config = null, bool recursive = false)
        {
            return ApplyFromDirectory(info.FullName, config, recursive);
        }

        /// <summary>
        /// Parses the json.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <returns>CiConfigObject.</returns>
        public static JsonConfigObject ParseJson(string json)
        {
            var lines = json.Split(new char[] { '\n' });
            // remove lines that start with a dash # character 
            var filtered = from l in lines
                           where !(Regex.IsMatch(l, @"^\s*#(.*)"))
                           select l;

            var filteredJson = string.Join("\n", filtered);

            var parsed = JsonConvert.DeserializeObject<ExpandoObject>(filteredJson, new ExpandoObjectConverter());

            // transform the ExpandoObject to the format expected by CiConfigObject
            parsed = JsonNetAdapter.Transform(parsed);

            // convert the ExpandoObject to CiConfigObject before returning
            var result = JsonConfigObject.FromExpandObject(parsed);
            return result;
        }

        /// <summary>
        /// overrides any default config specified in default.conf
        /// </summary>
        /// <param name="config">The configuration.</param>
        public static void SetDefaultConfig(dynamic config)
        {
            Default = config;

            // invalidate the Global config, forcing a re-merge next time its accessed
            globalConfig = null;
        }

        /// <summary>
        /// Sets the user configuration.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public static void SetUserConfig(JsonConfigObject config)
        {
            User = config;

            // disable the watcher
            if (userConfigWatcher != null)
            {
                userConfigWatcher.EnableRaisingEvents = false;
                userConfigWatcher.Dispose();
                userConfigWatcher = null;
            }

            // invalidate the Global config, forcing a re-merge next time its accessed
            globalConfig = null;
        }

        /// <summary>
        /// Gets the default configuration.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>dynamic.</returns>
        private static dynamic GetDefaultConfig(Assembly assembly)
        {
            var dconfJson = ScanForDefaultConfig(assembly);
            if (dconfJson == null)
                return null;
            return ParseJson(dconfJson);
        }

        /// <summary>
        /// Scans for default configuration.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>System.String.</returns>
        private static string ScanForDefaultConfig(Assembly assembly)
        {
            if (assembly == null)
                assembly = System.Reflection.Assembly.GetEntryAssembly();

            string[] res;
            try
            {
                // this might fail for the 'Anonymously Hosted DynamicMethods Assembly' created by an Reflect.Emit()
                res = assembly.GetManifestResourceNames();
            }
            catch
            {
                // for those assemblies, we don't provide a config
                return null;
            }
            var dconfResource =
                res.FirstOrDefault(
                    r =>
                    r.EndsWith("JsonConfig.conf", StringComparison.OrdinalIgnoreCase)
                    || r.EndsWith("JsonConfig.json", StringComparison.OrdinalIgnoreCase)
                    || r.EndsWith("JsonConfig.conf.json", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(dconfResource))
                return null;

            var stream = assembly.GetManifestResourceStream(dconfResource);
            string defaultJson = new StreamReader(stream).ReadToEnd();
            return defaultJson;
        }
    }
}
