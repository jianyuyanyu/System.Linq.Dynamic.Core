﻿using System.Collections.Generic;
using System.Linq.Dynamic.Core.Validation;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Linq.Dynamic.Core.CustomTypeProviders;

/// <summary>
/// The default implementation for <see cref="DefaultDynamicLinqCustomTypeProvider"/>.
/// 
/// Scans the current AppDomain for all types marked with <see cref="DynamicLinqTypeAttribute"/>, and adds them as custom Dynamic Link types.
///
/// This class is used as default for full .NET Framework and .NET Core App 2.x and higher.
/// </summary>
public class DefaultDynamicLinqCustomTypeProvider : AbstractDynamicLinqCustomTypeProvider, IDynamicLinqCustomTypeProvider
{
    private readonly IAssemblyHelper _assemblyHelper;
    private readonly bool _cacheCustomTypes;

    private HashSet<Type>? _cachedCustomTypes;
    private Dictionary<Type, List<MethodInfo>>? _cachedExtensionMethods;

     /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDynamicLinqCustomTypeProvider"/> class.
    /// Backwards compatibility for issue https://github.com/zzzprojects/System.Linq.Dynamic.Core/issues/830.
    /// </summary>
    /// <param name="cacheCustomTypes">Defines whether to cache the CustomTypes (including extension methods) which are found in the Application Domain. Default set to 'true'.</param>
    [Obsolete("Please use the DefaultDynamicLinqCustomTypeProvider(ParsingConfig config, IList<Type> additionalTypes, bool cacheCustomTypes = true) constructor.")]
    public DefaultDynamicLinqCustomTypeProvider(bool cacheCustomTypes = true) : this(ParsingConfig.Default, cacheCustomTypes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDynamicLinqCustomTypeProvider"/> class.
    /// </summary>
    /// <param name="config">The parsing configuration.</param>
    /// <param name="cacheCustomTypes">Defines whether to cache the CustomTypes (including extension methods) which are found in the Application Domain. Default set to 'true'.</param>
    public DefaultDynamicLinqCustomTypeProvider(ParsingConfig config, bool cacheCustomTypes = true) : this(config, new List<Type>(), cacheCustomTypes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDynamicLinqCustomTypeProvider"/> class.
    /// </summary>
    /// <param name="config">The parsing configuration.</param>
    /// <param name="additionalTypes">A list of additional types (without the DynamicLinqTypeAttribute annotation) which should also be resolved.</param>
    /// <param name="cacheCustomTypes">Defines whether to cache the CustomTypes (including extension methods) which are found in the Application Domain. Default set to 'true'.</param>
    public DefaultDynamicLinqCustomTypeProvider(ParsingConfig config, IList<Type> additionalTypes, bool cacheCustomTypes = true) : base(additionalTypes)
    {
        _assemblyHelper = new DefaultAssemblyHelper(Check.NotNull(config));
        _cacheCustomTypes = cacheCustomTypes;
    }

    /// <inheritdoc cref="IDynamicLinqCustomTypeProvider.GetCustomTypes"/>
    public virtual HashSet<Type> GetCustomTypes()
    {
        if (_cacheCustomTypes)
        {
            if (_cachedCustomTypes == null)
            {
                _cachedCustomTypes = GetCustomTypesInternal();
            }

            return _cachedCustomTypes;
        }

        return GetCustomTypesInternal();
    }

    /// <inheritdoc cref="IDynamicLinqCustomTypeProvider.GetExtensionMethods"/>
    public Dictionary<Type, List<MethodInfo>> GetExtensionMethods()
    {
        if (_cacheCustomTypes)
        {
            if (_cachedExtensionMethods == null)
            {
                _cachedExtensionMethods = GetExtensionMethodsInternal();
            }

            return _cachedExtensionMethods;
        }

        return GetExtensionMethodsInternal();
    }

    /// <inheritdoc cref="IDynamicLinqCustomTypeProvider.ResolveType"/>
    public Type? ResolveType(string typeName)
    {
        Check.NotEmpty(typeName);

        IEnumerable<Assembly> assemblies = _assemblyHelper.GetAssemblies();
        return ResolveType(assemblies, typeName);
    }

    /// <inheritdoc cref="IDynamicLinqCustomTypeProvider.ResolveTypeBySimpleName"/>
    public Type? ResolveTypeBySimpleName(string simpleTypeName)
    {
        Check.NotEmpty(simpleTypeName);

        IEnumerable<Assembly> assemblies = _assemblyHelper.GetAssemblies();
        return ResolveTypeBySimpleName(assemblies, simpleTypeName);
    }

    private HashSet<Type> GetCustomTypesInternal()
    {
        IEnumerable<Assembly> assemblies = _assemblyHelper.GetAssemblies();
        var types = FindTypesMarkedWithDynamicLinqTypeAttribute(assemblies).Union(AdditionalTypes);
        return new HashSet<Type>(types);
    }

    private Dictionary<Type, List<MethodInfo>> GetExtensionMethodsInternal()
    {
        var types = GetCustomTypes();

        var list = new List<Tuple<Type, MethodInfo>>();

        foreach (var type in types)
        {
            var extensionMethods = type
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => x.IsDefined(typeof(ExtensionAttribute), false))
                .ToList();

            extensionMethods.ForEach(x => list.Add(new Tuple<Type, MethodInfo>(x.GetParameters()[0].ParameterType, x)));
        }

        return list.GroupBy(x => x.Item1, tuple => tuple.Item2).ToDictionary(key => key.Key, methods => methods.ToList());
    }
}