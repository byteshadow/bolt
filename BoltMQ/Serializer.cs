using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BoltMQ.Core;
using BoltMQ.Core.Interfaces;

namespace BoltMQ
{
    public class Serializer : ISerializer
    {
        private const ushort UshortSize = sizeof(ushort);
        readonly Dictionary<Type, ushort> _typeToId = new Dictionary<Type, ushort>();
        readonly Dictionary<ushort, Type> _idToType = new Dictionary<ushort, Type>();
        readonly Dictionary<string, Type> _stringToType = new Dictionary<string, Type>();

        private bool _disposed;

        public Serializer()
        {
            //_payloadPool = new Pool<PayloadParser>(10, () => new PayloadParser());

            var catalog = new AggregateCatalog();
            string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (!string.IsNullOrEmpty(location))
                catalog.Catalogs.Add(new DirectoryCatalog(location));

            var container = new CompositionContainer(catalog);

            var msgs = container.GetExports<IMessage>();
            foreach (Lazy<IMessage> msg in msgs)
            {
                ResolveType(msg.Value.GetType());
            }
        }

        public object Deserialize(byte[] buffer)
        {
            PayloadParser payloadParser = new PayloadParser(buffer);

            Type dataType = ResolveType(payloadParser.BodyDataType);

            if (dataType == null)
                return null;

            FrameSegement data = payloadParser.Body.Data;

            using (MemoryStream stream = new MemoryStream(data.Buffer, data.Offset, data.Length))
            {
                return ProtoBuf.Serializer.NonGeneric.Deserialize(dataType, stream);
            }
        }

        public Type ResolveType(Frame dataType)
        {
            if (dataType.Data.Length == UshortSize)
            {
                ushort typeId = BitConverter.ToUInt16(dataType.Data.Buffer, dataType.Data.Offset);

                return _idToType.ContainsKey(typeId) ? _idToType[typeId] : null;
            }

            string fullName = Encoding.UTF8.GetString(dataType.Data.Buffer, dataType.Data.Offset, dataType.Data.Length);

            if (_stringToType.ContainsKey(fullName))
                return _stringToType[fullName];

            Type type = Type.GetType(fullName);

            if (type == null)
                return null;

            _stringToType.Add(fullName, type);
            return type;
        }

        public ushort ResolveType(Type type)
        {
            if (_typeToId.ContainsKey(type))
                return _typeToId[type];

            MessageAttribute messageAttribute = type.GetCustomAttributes(true).OfType<MessageAttribute>().FirstOrDefault();

            if (messageAttribute != null)
            {
                AddType(type, messageAttribute.Id);
                return messageAttribute.Id;
            }

            return 0;
        }

        public Type ResolveType(ushort typeId)
        {
            return _idToType.ContainsKey(typeId) ? _idToType[typeId] : null;
        }

        private void AddType(Type type, ushort id)
        {
            _typeToId.Add(type, id);
            _idToType.Add(id, type);
        }

        public byte[] Serialize<T>(T entity)
        {
            PayloadParser payloadParser = new PayloadParser();

            Frame dataTypeFrame = payloadParser.BodyDataType;
            if (!SetDataTypeFrame(entity.GetType(), dataTypeFrame))
                return null;

            Frame dataFrame = payloadParser.Body;
            SetDataFrame(entity, dataFrame);

            FrameSegement sizeSegment = payloadParser.Size;
            sizeSegment.Buffer = BitConverter.GetBytes(dataTypeFrame.Length + dataFrame.Length);
            sizeSegment.Length = sizeSegment.Buffer.Length;

            return payloadParser.Build();
        }

        private bool SetDataTypeFrame(Type type, Frame dataTypeFrame)
        {
            FrameSegement dataSegement = dataTypeFrame.Data;
            FrameSegement sizeSegement = dataTypeFrame.Size;

            //Check if the type is a known type
            ushort typeId = ResolveType(type);
            byte[] typeInBytes;

            if (typeId > 0)
            {
                typeInBytes = BitConverter.GetBytes(typeId);
            }
            else if (!string.IsNullOrEmpty(type.FullName))
            {
                typeInBytes = Encoding.UTF8.GetBytes(type.FullName);
            }
            else
            {
                return false;
            }

            dataSegement.Buffer = typeInBytes;
            dataSegement.Length = dataSegement.Buffer.Length;

            sizeSegement.Buffer = BitConverter.GetBytes(typeInBytes.Length);
            sizeSegement.Length = sizeSegement.Buffer.Length;
            return true;
        }

        private static void SetDataFrame<T>(T entity, Frame dataFrame)
        {
            FrameSegement dataSegement = dataFrame.Data;
            FrameSegement sizeSegement = dataFrame.Size;

            ProtoBufSerialize(entity, dataSegement);

            //Set the Data Size
            sizeSegement.Buffer = BitConverter.GetBytes(dataSegement.Length);
            sizeSegement.Length = sizeSegement.Buffer.Length;
        }

        private static void ProtoBufSerialize<T>(T entity, FrameSegement dataSegement)
        {
            using (MemoryStream stream = new MemoryStream())
            {

                ProtoBuf.Serializer.Serialize(stream, entity);
                int length = (int)stream.Length;

                dataSegement.Buffer = new byte[length];
                dataSegement.Length = length;

                stream.Position = 0;
                stream.Read(dataSegement.Buffer, 0, length);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            _stringToType.Clear();
            _idToType.Clear();
            _typeToId.Clear();

            _disposed = true;
        }

        ~Serializer()
        {
            Dispose(false);
        }
    }
}