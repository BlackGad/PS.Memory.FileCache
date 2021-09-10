using System;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.Serialization.Formatters.Binary;
using PS.Runtime.Caching.API;
using PS.Runtime.Caching.Extensions;

namespace PS.Runtime.Caching.Default
{
    public class DefaultDataSerializer : IDataSerializer
    {
        #region Constants

        private static readonly string ObjectTypeAssemblyQualifiedName;

        #endregion

        private readonly BinaryFormatter _binaryFormatter;

        #region Constructors

        static DefaultDataSerializer()
        {
            // ReSharper disable once PossibleNullReferenceException
            ObjectTypeAssemblyQualifiedName = string.Join(",", typeof(object).AssemblyQualifiedName.Split(',').Take(2));
        }

        public DefaultDataSerializer()
        {
            _binaryFormatter = new BinaryFormatter();
        }

        #endregion

        #region IDataSerializer Members

        /// <summary>
        ///     Deserialize CacheItem from byte array
        /// </summary>
        public virtual CacheItem DeserializeItem(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                //Item
                var key = reader.ReadString();
                var regionName = reader.ReadString();
                var type = Type.GetType(reader.ReadString());
                var dataCount = reader.ReadInt32();

                object value = null;
                if (dataCount != 0)
                {
                    var serializedData = reader.ReadBytes(dataCount);
                    value = DeserializeData(type, serializedData);
                }

                return new CacheItem(key, value, regionName);
            }
        }

        /// <summary>
        ///     Serialize CacheItem item to byte array
        /// </summary>
        public virtual byte[] SerializeItem(CacheItem item)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                //Item
                writer.Write(item.Key);
                writer.Write(item.RegionName ?? string.Empty);

                if (item.Value != null)
                {
                    var type = item.Value.GetType();
                    if (type.AssemblyQualifiedName == null)
                    {
                        writer.Write(ObjectTypeAssemblyQualifiedName);
                    }
                    else
                    {
                        var qualifiedType = type.GetAssemblyQualifiedName();
                        writer.Write(qualifiedType);
                    }

                    var serializedData = SerializeData(type, item.Value);
                    writer.Write(serializedData.Length);
                    writer.Write(serializedData);
                }
                else
                {
                    writer.Write(ObjectTypeAssemblyQualifiedName);
                    writer.Write(0);
                }

                stream.Seek(0, SeekOrigin.Begin);
                return stream.ToArray();
            }
        }

        #endregion

        #region Members

        /// <summary>
        ///     Deserialize data object from byte array
        /// </summary>
        protected virtual object DeserializeData(Type type, byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                #pragma warning disable SYSLIB0011 // Type or member is obsolete
                #pragma warning disable 618
                return _binaryFormatter.Deserialize(stream);
                #pragma warning restore 618
                #pragma warning restore SYSLIB0011 // Type or member is obsolete
            }
        }

        /// <summary>
        ///     Serialize data object to byte array
        /// </summary>
        protected virtual byte[] SerializeData(Type type, object data)
        {
            using (var stream = new MemoryStream())
            {
                #pragma warning disable SYSLIB0011 // Type or member is obsolete
                #pragma warning disable 618
                _binaryFormatter.Serialize(stream, data);
                #pragma warning restore 618
                #pragma warning restore SYSLIB0011 // Type or member is obsolete

                stream.Seek(0, SeekOrigin.Begin);
                return stream.ToArray();
            }
        }

        #endregion
    }
}