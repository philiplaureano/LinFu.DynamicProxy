using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace LinFu.DynamicProxy
{
#if !SILVERLIGHT
    [Serializable]
    public class ProxyObjectReference : IObjectReference, ISerializable
    {
        private readonly Type _baseType;
        private readonly IProxy _proxy;

        protected ProxyObjectReference(SerializationInfo info, StreamingContext context)
        {
            // Deserialize the base type using its assembly qualified name
            var qualifiedName = info.GetString("__baseType");
            _baseType = Type.GetType(qualifiedName, true, false);

            // Rebuild the list of interfaces
            var interfaceList = new List<Type>();
            var interfaceCount = info.GetInt32("__baseInterfaceCount");
            for (var i = 0; i < interfaceCount; i++)
            {
                var keyName = string.Format("__baseInterface{0}", i);
                var currentQualifiedName = info.GetString(keyName);
                var interfaceType = Type.GetType(currentQualifiedName, true, false);

                interfaceList.Add(interfaceType);
            }

            // Reconstruct the proxy
            var factory = new ProxyFactory();
            var proxyType = factory.CreateProxyType(_baseType, interfaceList.ToArray());

            // Initialize the proxy with the deserialized data
            object[] args = {info, context};
            _proxy = (IProxy) Activator.CreateInstance(proxyType, args);
        }

        #region IObjectReference Members

        public object GetRealObject(StreamingContext context)
        {
            return _proxy;
        }

        #endregion

        #region ISerializable Members

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
        }

        #endregion
    }
#endif
}