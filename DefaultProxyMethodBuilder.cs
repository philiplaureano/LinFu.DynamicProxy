using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace LinFu.DynamicProxy
{
    internal class DefaultyProxyMethodBuilder : IProxyMethodBuilder
    {
        public DefaultyProxyMethodBuilder() : this(new DefaultMethodEmitter())
        {
        }

        public DefaultyProxyMethodBuilder(IMethodBodyEmitter emitter)
        {
            MethodBodyEmitter = emitter;
        }

        public IMethodBodyEmitter MethodBodyEmitter { get; set; }

        #region IProxyMethodBuilder Members

        public void CreateProxiedMethod(FieldInfo field, MethodInfo method, TypeBuilder typeBuilder)
        {
            var parameters = method.GetParameters();
            var parameterTypes = new List<Type>();
            foreach (var param in parameters) parameterTypes.Add(param.ParameterType);

            var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig |
                                   MethodAttributes.Virtual;
            var methodBuilder = typeBuilder.DefineMethod(method.Name, methodAttributes,
                CallingConventions.HasThis, method.ReturnType,
                parameterTypes.ToArray());

            var typeArgs = method.GetGenericArguments();

            if (typeArgs != null && typeArgs.Length > 0)
            {
                var typeNames = new List<string>();

                for (var index = 0; index < typeArgs.Length; index++) typeNames.Add(string.Format("T{0}", index));

                methodBuilder.DefineGenericParameters(typeNames.ToArray());
            }

            var IL = methodBuilder.GetILGenerator();

            Debug.Assert(MethodBodyEmitter != null);
            MethodBodyEmitter.EmitMethodBody(IL, method, field);
        }

        #endregion
    }
}