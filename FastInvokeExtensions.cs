using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;

namespace LinFu.DynamicProxy
{
    public static class FastInvokeExtensions
    {
        public static object FastInvoke(this MethodInfo method, object instance, params object[] arguments)
        {
            var returnType = method.ReturnType;
            if (returnType == typeof(void))
            {
                var action = method.CreateAction();
                action(instance, arguments);
                return null;
            }

            var func = method.CreateFunc();
            var result = func(instance, arguments);

            return result;
        }

        private static Action<object, object[]> CreateAction(this MethodInfo method)
        {
            var (p1, p2, call) = MakeCallExpressions(method);

            var lambda = Expression.Lambda<Action<object, object[]>>(Expression.Block(call), p1, p2);
            return lambda.CompileFast();
        }

        private static Func<object, object[], object> CreateFunc(this MethodInfo method)
        {
            var (p1, p2, call) = MakeCallExpressions(method);

            var lambda = Expression.Lambda<Func<object, object[], object>>(call, p1, p2);
            return lambda.CompileFast();
        }

        private static (ParameterExpression, ParameterExpression, Expression) MakeCallExpressions(MethodInfo method)
        {
            var index = 0;
            var p1 = Expression.Parameter(typeof(object), "instance");
            var p2 = Expression.Parameter(typeof(object[]), "parameters");
            var parameters = method.GetParameters()
                .Select(p =>
                    Expression.Convert(Expression.ArrayAccess(p2, Expression.Constant(index++)), p.ParameterType));

            var call = method.IsStatic
                ? Expression.Call(method, parameters)
                : Expression.Call(Expression.Convert(p1, method.DeclaringType), method, parameters);

            return (p1, p2, call);
        }
    }
}