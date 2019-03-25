using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace LinFu.DynamicProxy
{
    public class ProxyFactory
    {
        private static readonly ConstructorInfo baseConstructor = typeof(object).GetConstructor(new Type[0]);
        private static readonly MethodInfo getTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle");

        public ProxyFactory()
            : this(new DefaultyProxyMethodBuilder())
        {
        }

        public ProxyFactory(IProxyMethodBuilder proxyMethodBuilder)
        {
            ProxyMethodBuilder = proxyMethodBuilder;
        }

        public IProxyCache Cache { get; set; } = new ProxyCache();

        public IProxyMethodBuilder ProxyMethodBuilder { get; set; }

        public virtual object CreateProxy(Type instanceType, IInvokeWrapper wrapper, params Type[] baseInterfaces)
        {
            return CreateProxy(instanceType, new CallAdapter(wrapper), baseInterfaces);
        }

        public virtual object CreateProxy(Type instanceType, IInterceptor interceptor, params Type[] baseInterfaces)
        {
            var proxyType = CreateProxyType(instanceType, baseInterfaces);
            var result = Activator.CreateInstance(proxyType);
            var proxy = (IProxy) result;
            proxy.Interceptor = interceptor;

            return result;
        }

        public virtual T CreateProxy<T>(IInvokeWrapper wrapper, params Type[] baseInterfaces)
        {
            return CreateProxy<T>(new CallAdapter(wrapper), baseInterfaces);
        }

        public virtual T CreateProxy<T>(IInterceptor interceptor, params Type[] baseInterfaces)
        {
            var proxyType = CreateProxyType(typeof(T), baseInterfaces);
            var result = (T) Activator.CreateInstance(proxyType);
            Debug.Assert(result != null);

            var proxy = (IProxy) result;
            proxy.Interceptor = interceptor;

            return result;
        }

        public virtual Type CreateProxyType(Type baseType, params Type[] baseInterfaces)
        {
            // Reuse the previous results, if possible
            if (Cache != null && Cache.Contains(baseType, baseInterfaces))
                return Cache.GetProxyType(baseType, baseInterfaces);

            var result = CreateUncachedProxyType(baseInterfaces, baseType);

            // Cache the proxy type
            if (result != null && Cache != null)
                Cache.StoreProxyType(result, baseType, baseInterfaces);

            return result;
        }

        private Type CreateUncachedProxyType(Type[] baseInterfaces, Type baseType)
        {
            var currentDomain = AppDomain.CurrentDomain;
            var typeName = string.Format("{0}Proxy", baseType.Name);
            var assemblyName = string.Format("{0}Assembly", typeName);
            var moduleName = string.Format("{0}Module", typeName);

            var name = new AssemblyName(assemblyName);
            var access = AssemblyBuilderAccess.Run;
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, access);

            var moduleBuilder = assemblyBuilder.DefineDynamicModule(moduleName);

            var typeAttributes = TypeAttributes.AutoClass | TypeAttributes.Class |
                                 TypeAttributes.Public | TypeAttributes.BeforeFieldInit;

            var interfaceList = new List<Type>();
            if (baseInterfaces != null && baseInterfaces.Length > 0)
                interfaceList.AddRange(baseInterfaces);


            // Use the proxy dummy as the base type 
            // since we're not inheriting from any class type
            var parentType = baseType;
            if (baseType.IsInterface)
            {
                parentType = typeof(ProxyDummy);
                interfaceList.Add(baseType);
            }

            // Add any inherited interfaces
            var interfaces = interfaceList.ToArray();
            foreach (var interfaceType in interfaces) BuildInterfaceList(interfaceType, interfaceList);

#if !SILVERLIGHT
            // Add the ISerializable interface so that it can be implemented
            if (!interfaceList.Contains(typeof(ISerializable)))
                interfaceList.Add(typeof(ISerializable));
#endif
            var typeBuilder =
                moduleBuilder.DefineType(typeName, typeAttributes, parentType, interfaceList.ToArray());

            var defaultConstructor = DefineConstructor(typeBuilder);

            // Implement IProxy
            var implementor = new ProxyImplementor();
            implementor.ImplementProxy(typeBuilder);

            var methods = baseType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            var proxyList = new List<MethodInfo>();
            BuildMethodList(interfaceList, methods, proxyList);


            Debug.Assert(ProxyMethodBuilder != null, "ProxyMethodBuilder cannot be null");

            FieldInfo interceptorField = implementor.InterceptorField;
            foreach (var method in proxyList)
            {
#if !SILVERLIGHT
                // Provide a custom implementation of ISerializable
                // instead of redirecting it back to the interceptor
                if (method.DeclaringType == typeof(ISerializable))
                    continue;
#endif
                ProxyMethodBuilder.CreateProxiedMethod(interceptorField, method, typeBuilder);
            }

#if !SILVERLIGHT
            // Make the proxy serializable
            AddSerializationSupport(baseType, baseInterfaces, typeBuilder, interceptorField, defaultConstructor);
#endif
            Type proxyType = typeBuilder.CreateTypeInfo();

#if DEBUG_PROXY_OUTPUT
            assemblyBuilder.Save("generatedAssembly.dll");
#endif
            return proxyType;
        }

        private static void BuildInterfaceList(Type currentType, List<Type> interfaceList)
        {
            var interfaces = currentType.GetInterfaces();
            if (interfaces.Length == 0)
                return;

            foreach (var current in interfaces)
            {
                if (interfaceList.Contains(current))
                    continue;

                interfaceList.Add(current);
                BuildInterfaceList(current, interfaceList);
            }
        }

        private static void BuildMethodList(IEnumerable<Type> interfaceList, IEnumerable<MethodInfo> methods,
            List<MethodInfo> proxyList)
        {
            foreach (var method in methods)
            {
                //if (method.DeclaringType == typeof(object))
                //    continue;

                // Only non-private methods will be proxied
                if (method.IsPrivate)
                    continue;

                // Final methods cannot be overridden
                if (method.IsFinal)
                    continue;

                // Only virtual methods can be intercepted
                if (!method.IsVirtual && !method.IsAbstract)
                    continue;

                proxyList.Add(method);
            }

            foreach (var interfaceType in interfaceList)
            {
                var interfaceMethods = interfaceType.GetMethods();
                foreach (var interfaceMethod in interfaceMethods)
                {
                    if (proxyList.Contains(interfaceMethod))
                        continue;

                    proxyList.Add(interfaceMethod);
                }
            }
        }

        private static ConstructorBuilder DefineConstructor(TypeBuilder typeBuilder)
        {
            var constructorAttributes = MethodAttributes.Public |
                                        MethodAttributes.HideBySig | MethodAttributes.SpecialName |
                                        MethodAttributes.RTSpecialName;

            var constructor =
                typeBuilder.DefineConstructor(constructorAttributes, CallingConventions.Standard, new Type[] { });

            var IL = constructor.GetILGenerator();

            constructor.SetImplementationFlags(MethodImplAttributes.IL | MethodImplAttributes.Managed);

            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Call, baseConstructor);
            IL.Emit(OpCodes.Ret);

            return constructor;
        }
#if !SILVERLIGHT
        private static readonly MethodInfo getValue = typeof(SerializationInfo).GetMethod("GetValue",
            BindingFlags.Public | BindingFlags.Instance, null, new[] {typeof(string), typeof(Type)}, null);

        private static readonly MethodInfo setType = typeof(SerializationInfo).GetMethod("SetType",
            BindingFlags.Public | BindingFlags.Instance, null, new[] {typeof(Type)}, null);

        private static readonly MethodInfo addValue = typeof(SerializationInfo).GetMethod("AddValue",
            BindingFlags.Public | BindingFlags.Instance, null, new[] {typeof(string), typeof(object)}, null);
#endif

#if !SILVERLIGHT
        private static void ImplementGetObjectData(Type baseType, Type[] baseInterfaces, TypeBuilder typeBuilder,
            FieldInfo interceptorField)
        {
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig |
                             MethodAttributes.Virtual;
            Type[] parameterTypes = {typeof(SerializationInfo), typeof(StreamingContext)};

            var methodBuilder =
                typeBuilder.DefineMethod("GetObjectData", attributes, typeof(void), parameterTypes);

            var IL = methodBuilder.GetILGenerator();
            //LocalBuilder proxyBaseType = IL.DeclareLocal(typeof(Type));

            // info.SetType(typeof(ProxyObjectReference));
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Ldtoken, typeof(ProxyObjectReference));
            IL.Emit(OpCodes.Call, getTypeFromHandle);
            IL.Emit(OpCodes.Callvirt, setType);

            // info.AddValue("__interceptor", __interceptor);
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Ldstr, "__interceptor");
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldfld, interceptorField);
            IL.Emit(OpCodes.Callvirt, addValue);

            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Ldstr, "__baseType");
            IL.Emit(OpCodes.Ldstr, baseType.AssemblyQualifiedName);
            IL.Emit(OpCodes.Callvirt, addValue);

            var interfaces = baseInterfaces ?? new Type[0];
            var baseInterfaceCount = interfaces.Length;

            // Save the number of base interfaces
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Ldstr, "__baseInterfaceCount");
            IL.Emit(OpCodes.Ldc_I4, baseInterfaceCount);
            IL.Emit(OpCodes.Box, typeof(int));
            IL.Emit(OpCodes.Callvirt, addValue);

            var index = 0;
            foreach (var baseInterface in interfaces)
            {
                IL.Emit(OpCodes.Ldarg_1);
                IL.Emit(OpCodes.Ldstr, string.Format("__baseInterface{0}", index++));
                IL.Emit(OpCodes.Ldstr, baseInterface.AssemblyQualifiedName);
                IL.Emit(OpCodes.Callvirt, addValue);
            }

            IL.Emit(OpCodes.Ret);
        }

        private static void DefineSerializationConstructor(Type[] baseInterfaces, TypeBuilder typeBuilder,
            FieldInfo interceptorField, ConstructorBuilder defaultConstructor)
        {
            var constructorAttributes = MethodAttributes.Public |
                                        MethodAttributes.HideBySig | MethodAttributes.SpecialName |
                                        MethodAttributes.RTSpecialName;

            Type[] parameterTypes = {typeof(SerializationInfo), typeof(StreamingContext)};
            var constructor = typeBuilder.DefineConstructor(constructorAttributes,
                CallingConventions.Standard, parameterTypes);

            var IL = constructor.GetILGenerator();

            var interceptorType = IL.DeclareLocal(typeof(Type));
            //LocalBuilder interceptor = IL.DeclareLocal(typeof(IInterceptor));

            constructor.SetImplementationFlags(MethodImplAttributes.IL | MethodImplAttributes.Managed);


            IL.Emit(OpCodes.Ldtoken, typeof(IInterceptor));
            IL.Emit(OpCodes.Call, getTypeFromHandle);
            IL.Emit(OpCodes.Stloc, interceptorType);

            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Call, defaultConstructor);

            // __interceptor = (IInterceptor)info.GetValue("__interceptor", typeof(IInterceptor));
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Ldstr, "__interceptor");
            IL.Emit(OpCodes.Ldloc, interceptorType);
            IL.Emit(OpCodes.Callvirt, getValue);
            IL.Emit(OpCodes.Castclass, typeof(IInterceptor));
            IL.Emit(OpCodes.Stfld, interceptorField);

            IL.Emit(OpCodes.Ret);
        }

        private static void AddSerializationSupport(Type baseType, Type[] baseInterfaces, TypeBuilder typeBuilder,
            FieldInfo interceptorField, ConstructorBuilder defaultConstructor)
        {
            var serializableConstructor = typeof(SerializableAttribute).GetConstructor(new Type[0]);
            var customAttributeBuilder = new CustomAttributeBuilder(serializableConstructor, new object[0]);
            typeBuilder.SetCustomAttribute(customAttributeBuilder);

            DefineSerializationConstructor(baseInterfaces, typeBuilder, interceptorField, defaultConstructor);
            ImplementGetObjectData(baseType, baseInterfaces, typeBuilder, interceptorField);
        }
#endif
    }
}