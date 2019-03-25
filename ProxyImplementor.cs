using System;
using System.Reflection;
using System.Reflection.Emit;

namespace LinFu.DynamicProxy
{
    internal class ProxyImplementor
    {
        public FieldBuilder InterceptorField { get; private set; }

        public void ImplementProxy(TypeBuilder typeBuilder)
        {
            // Implement the IProxy interface
            typeBuilder.AddInterfaceImplementation(typeof(IProxy));

            InterceptorField = typeBuilder.DefineField("__interceptor", typeof(IInterceptor),
                FieldAttributes.Private);

            // Implement the getter
            var attributes = MethodAttributes.Public | MethodAttributes.HideBySig |
                             MethodAttributes.SpecialName | MethodAttributes.NewSlot |
                             MethodAttributes.Virtual;

            // Implement the getter
            var getterMethod = typeBuilder.DefineMethod("get_Interceptor", attributes,
                CallingConventions.HasThis, typeof(IInterceptor),
                new Type[0]);
            getterMethod.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.IL);

            var IL = getterMethod.GetILGenerator();

            // This is equivalent to:
            // get { return __interceptor;
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldfld, InterceptorField);
            IL.Emit(OpCodes.Ret);

            // Implement the setter
            var setterMethod = typeBuilder.DefineMethod("set_Interceptor", attributes,
                CallingConventions.HasThis, typeof(void),
                new[] {typeof(IInterceptor)});

            setterMethod.SetImplementationFlags(MethodImplAttributes.Managed | MethodImplAttributes.IL);
            IL = setterMethod.GetILGenerator();
            IL.Emit(OpCodes.Ldarg_0);
            IL.Emit(OpCodes.Ldarg_1);
            IL.Emit(OpCodes.Stfld, InterceptorField);
            IL.Emit(OpCodes.Ret);

            var originalSetter = typeof(IProxy).GetMethod("set_Interceptor");
            var originalGetter = typeof(IProxy).GetMethod("get_Interceptor");

            typeBuilder.DefineMethodOverride(setterMethod, originalSetter);
            typeBuilder.DefineMethodOverride(getterMethod, originalGetter);
        }
    }
}