using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace LinFu.DynamicProxy
{
    public class InvocationInfo
    {
        public InvocationInfo(object proxy, MethodInfo targetMethod,
            StackTrace trace, Type[] genericTypeArgs, object[] args)
        {
            Target = proxy;
            TargetMethod = targetMethod;
            TypeArguments = genericTypeArgs;
            Arguments = args;
            StackTrace = trace;
        }

        public object Target { get; }

        public MethodInfo TargetMethod { get; }

        public StackTrace StackTrace { get; }

        public MethodInfo CallingMethod => (MethodInfo) StackTrace.GetFrame(0).GetMethod();

        public Type[] TypeArguments { get; }

        public object[] Arguments { get; }

        public void SetArgument(int position, object arg)
        {
            Arguments[position] = arg;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat("Calling Method: {0,30:G}\n", GetMethodName(CallingMethod));
            builder.AppendFormat("Target Method:{0,30:G}\n", GetMethodName(TargetMethod));
            builder.AppendLine("Arguments:");

            foreach (var info in TargetMethod.GetParameters())
            {
                var currentArgument = Arguments[info.Position];
                if (currentArgument == null)
                    currentArgument = "(null)";
                builder.AppendFormat("\t{0,10:G}: {1}\n", info.Name, currentArgument);
            }

            builder.AppendLine();

            return builder.ToString();
        }

        private string GetMethodName(MethodInfo method)
        {
            var builder = new StringBuilder();
            builder.AppendFormat("{0}.{1}", method.DeclaringType.Name, method.Name);
            builder.Append("(");

            var parameters = method.GetParameters();
            var parameterCount = parameters != null ? parameters.Length : 0;

            var index = 0;
            foreach (var param in parameters)
            {
                index++;
                builder.AppendFormat("{0} {1}", param.ParameterType.Name, param.Name);

                if (index < parameterCount)
                    builder.Append(", ");
            }

            builder.Append(")");

            return builder.ToString();
        }
    }
}