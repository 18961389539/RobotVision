using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using ImageViewer.Abstractions;
using ImageViewer.Logging;

namespace ImageViewer.Plugins
{
    public static class RoiPluginDiscoveryService
    {
        public static RoiPluginDiscoveryResult DiscoverAndRegister(RoiPluginRegistry? registry = null, string? directoryPath = null, IImageViewerLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            var options = new RoiPluginDiscoveryOptions { PluginDirectoryPath = directoryPath };
            return DiscoverAndRegister(options, registry, logger);
        }

        public static RoiPluginDiscoveryResult DiscoverAndRegister(RoiPluginDiscoveryOptions? options, RoiPluginRegistry? registry = null, IImageViewerLogger? logger = null)
        {
            var targetRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            var effectiveOptions = options ?? new RoiPluginDiscoveryOptions();
            string scanDirectory = effectiveOptions.ResolvePluginDirectory(AppContext.BaseDirectory);
            return RegisterFromAssemblies(LoadAssemblies(scanDirectory, effectiveOptions, logger), targetRegistry, effectiveOptions, logger);
        }

        public static RoiPluginDiscoveryResult RegisterFromAssemblies(IEnumerable<Assembly> assemblies, RoiPluginRegistry? registry = null, RoiPluginDiscoveryOptions? options = null, IImageViewerLogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            var targetRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            var effectiveOptions = options ?? new RoiPluginDiscoveryOptions();
            var disabledModuleTypeNames = new HashSet<string>(effectiveOptions.DisabledModuleTypeNames, StringComparer.OrdinalIgnoreCase);
            var registeredModuleTypeNames = new List<string>();
            var failures = new List<RoiPluginDiscoveryFailure>();

            var moduleTypes = assemblies
                .Distinct()
                .SelectMany(GetLoadableTypes)
                .Where(type =>
                    type is { IsAbstract: false, IsInterface: false } &&
                    typeof(IRoiPluginModule).IsAssignableFrom(type) &&
                    type.GetConstructor(Type.EmptyTypes) != null &&
                    !disabledModuleTypeNames.Contains(type.FullName ?? type.Name))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);

            foreach (var moduleType in moduleTypes)
            {
                RoiPluginDiscoveryFailure? failure = RegisterModule(
                    moduleType,
                    targetRegistry,
                    effectiveOptions,
                    logger);
                if (failure == null)
                {
                    registeredModuleTypeNames.Add(moduleType.FullName ?? moduleType.Name);
                }
                else
                {
                    failures.Add(failure);
                }
            }

            ApplyPluginFilters(targetRegistry, effectiveOptions, null);
            var result = new RoiPluginDiscoveryResult(registeredModuleTypeNames, failures);
            if (effectiveOptions.FailOnModuleRegistrationError && result.HasFailures)
            {
                throw new AggregateException(
                    "One or more ROI plugin modules failed to register.",
                    result.Failures.Select(failure => failure.Exception));
            }

            return result;
        }

        private static RoiPluginDiscoveryFailure? RegisterModule(
            Type moduleType,
            RoiPluginRegistry registry,
            RoiPluginDiscoveryOptions options,
            IImageViewerLogger? logger)
        {
            var beforeKeys = new HashSet<string>(registry.RegisteredTypeKeys, StringComparer.OrdinalIgnoreCase);
            var moduleName = moduleType.FullName ?? moduleType.Name;
            try
            {
                var module = (IRoiPluginModule)Activator.CreateInstance(moduleType)!;
                ImageViewerLoggerSupport.PluginRegisterStarted(logger, moduleName);
                module.Register(registry);
                ApplyPluginFilters(registry, options, beforeKeys);
                ImageViewerLoggerSupport.PluginRegisterSucceeded(logger, moduleName);
                return null;
            }
            catch (Exception ex)
            {
                RemoveNewRegistrations(registry, beforeKeys);
                ImageViewerLoggerSupport.PluginRegisterFailed(logger, moduleName, ex);
                return new RoiPluginDiscoveryFailure(moduleName, ex);
            }
        }

        private static void RemoveNewRegistrations(RoiPluginRegistry registry, HashSet<string> registeredBeforeModule)
        {
            foreach (string typeKey in registry.RegisteredTypeKeys
                .Where(key => !registeredBeforeModule.Contains(key))
                .ToArray())
            {
                registry.Unregister(typeKey);
            }
        }

        private static void ApplyPluginFilters(RoiPluginRegistry registry, RoiPluginDiscoveryOptions options, HashSet<string>? registeredBeforeModule)
        {
            var disabledKeys = new HashSet<string>(options.DisabledPluginTypeKeys, StringComparer.OrdinalIgnoreCase);
            disabledKeys.UnionWith(options.UnloadedPluginTypeKeys);

            if (disabledKeys.Count == 0)
            {
                return;
            }

            string[] keysToInspect = registeredBeforeModule == null
                ? registry.RegisteredTypeKeys.ToArray()
                : registry.RegisteredTypeKeys.Where(key => !registeredBeforeModule.Contains(key)).ToArray();

            foreach (var typeKey in keysToInspect)
            {
                if (disabledKeys.Contains(typeKey))
                {
                    registry.Unregister(typeKey);
                }
            }
        }

        private static IEnumerable<Assembly> LoadAssemblies(string directoryPath, RoiPluginDiscoveryOptions options, IImageViewerLogger? logger)
        {
            var disabledAssemblyNames = new HashSet<string>(options.DisabledAssemblyNames, StringComparer.OrdinalIgnoreCase);
            var allowedAssemblyNamePrefixes = options.AllowedAssemblyNamePrefixes
                .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
                .ToArray();
            var loadedAssemblies = options.ScanLoadedAssemblies
                ? AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .ToDictionary(assembly => assembly.FullName ?? assembly.GetName().Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in loadedAssemblies.Values)
            {
                yield return assembly;
            }

            if (!Directory.Exists(directoryPath))
            {
                yield break;
            }

            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly))
            {
                AssemblyName assemblyName;
                try
                {
                    assemblyName = AssemblyName.GetAssemblyName(filePath);
                }
                catch (Exception ex)
                {
                    ImageViewerLoggerSupport.PluginAssemblySkipped(logger, filePath, ex.Message);
                    continue;
                }

                string simpleName = assemblyName.Name ?? Path.GetFileNameWithoutExtension(filePath);
                if (disabledAssemblyNames.Contains(simpleName) ||
                    (allowedAssemblyNamePrefixes.Length > 0 &&
                     !allowedAssemblyNamePrefixes.Any(prefix => simpleName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                if (loadedAssemblies.Values.Any(assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName)))
                {
                    continue;
                }

                Assembly? assembly = null;
                try
                {
                    // 现状说明：使用 AssemblyLoadContext.Default 加载插件程序集，该上下文在进程生命周期内
                    // 不可卸载，因此插件 dll 会一直占用文件句柄与程序集引用。
                    // 风险：重复扫描/替换插件 dll 时旧版本不会被回收，也无法热更新插件。
                    // 若需支持卸载（Collectible ALC）改造风险较高（插件与宿主共享类型、事件委托、非托管依赖），
                    // 暂接受当前降级方案；如需热更新可在产品层通过独立进程承载插件规避。
                    assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);
                }
                catch (Exception ex)
                {
                    ImageViewerLoggerSupport.PluginAssemblyLoadFailed(logger, filePath, ex);
                }

                if (assembly != null)
                {
                    loadedAssemblies[assembly.FullName ?? assembly.GetName().Name ?? filePath] = assembly;
                    yield return assembly;
                }
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.OfType<Type>().ToArray();
            }
        }
    }
}
