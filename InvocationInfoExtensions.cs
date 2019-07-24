using System;

namespace LinFu.DynamicProxy
{
    public static class InvocationInfoExtensions
    {
        public static TResult Proceed<TResult>(this InvocationInfo info)
        {
            return (TResult) info.Proceed();
        }

        public static object Proceed(this InvocationInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            var targetInstance = info.Target;
            var targetMethod = info.TargetMethod;
            var arguments = info.Arguments;

            return targetMethod.Invoke(targetInstance, arguments);
        }
    }
}