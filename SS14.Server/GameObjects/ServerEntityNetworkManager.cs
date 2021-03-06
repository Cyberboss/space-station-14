﻿using Lidgren.Network;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.Network;
using SS14.Shared.Interfaces.Serialization;
using SS14.Shared.IoC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SS14.Server.GameObjects
{
    public class ServerEntityNetworkManager : IEntityNetworkManager
    {
        [Dependency]
        private readonly ISS14Serializer serializer;
        [Dependency]
        private readonly IServerNetManager _mNetManager;

        #region IEntityNetworkManager Members

        public NetOutgoingMessage CreateEntityMessage()
        {
            NetOutgoingMessage message = _mNetManager.Peer.CreateMessage();
            message.Write((byte)NetMessages.EntityMessage);
            return message;
        }

        public void SendToAll(NetOutgoingMessage message, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
        {
            _mNetManager.ServerSendToAll(message, method);
        }

        public void SendMessage(NetOutgoingMessage message, NetConnection recipient,
                                NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
        {
            _mNetManager.Peer.SendMessage(message, recipient, method);
        }

        /// <summary>
        /// Sends an arbitrary entity network message
        /// </summary>
        /// <param name="sendingEntity">The entity the message is going from(and to, on the other end)</param>
        /// <param name="type">Message type</param>
        /// <param name="list">List of parameter objects</param>
        public void SendEntityNetworkMessage(IEntity sendingEntity, EntityMessage type, params object[] list)
        {
        }

        public void SendComponentNetworkMessage(IEntity sendingEntity, uint netID,
                                                NetDeliveryMethod method = NetDeliveryMethod.ReliableUnordered, params object[] messageParams)
        {
            throw new NotImplementedException();
        }

        #endregion IEntityNetworkManager Members

        #region Sending

        /// <summary>
        /// Allows a component owned by this entity to send a message to a counterpart component on the
        /// counterpart entities on all clients.
        /// </summary>
        /// <param name="sendingEntity">Entity sending the message (also entity to send to)</param>
        /// <param name="family">Family of the component sending the message</param>
        /// <param name="method">Net delivery method -- if null, defaults to NetDeliveryMethod.ReliableUnordered</param>
        /// <param name="recipient">Client connection to send to. If null, send to all.</param>
        /// <param name="messageParams">Parameters of the message</param>
        public void SendDirectedComponentNetworkMessage(IEntity sendingEntity, uint netID,
                                                        NetDeliveryMethod method,
                                                        NetConnection recipient, params object[] messageParams)
        {
            NetOutgoingMessage message = CreateEntityMessage();
            message.Write((byte)EntityMessage.ComponentMessage);
            message.Write(sendingEntity.Uid); //Write this entity's UID
            message.Write(netID);
            //Loop through the params and write them as is proper
            foreach (object messageParam in messageParams)
            {
                if (messageParam.GetType().IsSubclassOf(typeof(Enum)))
                {
                    message.Write((byte)NetworkDataType.d_enum);
                    message.Write((int)messageParam);
                }
                else if (messageParam is bool)
                {
                    message.Write((byte)NetworkDataType.d_bool);
                    message.Write((bool)messageParam);
                }
                else if (messageParam is byte)
                {
                    message.Write((byte)NetworkDataType.d_byte);
                    message.Write((byte)messageParam);
                }
                else if (messageParam is sbyte)
                {
                    message.Write((byte)NetworkDataType.d_sbyte);
                    message.Write((sbyte)messageParam);
                }
                else if (messageParam is ushort)
                {
                    message.Write((byte)NetworkDataType.d_ushort);
                    message.Write((ushort)messageParam);
                }
                else if (messageParam is short)
                {
                    message.Write((byte)NetworkDataType.d_short);
                    message.Write((short)messageParam);
                }
                else if (messageParam is int)
                {
                    message.Write((byte)NetworkDataType.d_int);
                    message.Write((int)messageParam);
                }
                else if (messageParam is uint)
                {
                    message.Write((byte)NetworkDataType.d_uint);
                    message.Write((uint)messageParam);
                }
                else if (messageParam is ulong)
                {
                    message.Write((byte)NetworkDataType.d_ulong);
                    message.Write((ulong)messageParam);
                }
                else if (messageParam is long)
                {
                    message.Write((byte)NetworkDataType.d_long);
                    message.Write((long)messageParam);
                }
                else if (messageParam is float)
                {
                    message.Write((byte)NetworkDataType.d_float);
                    message.Write((float)messageParam);
                }
                else if (messageParam is double)
                {
                    message.Write((byte)NetworkDataType.d_double);
                    message.Write((double)messageParam);
                }
                else if (messageParam is string)
                {
                    message.Write((byte)NetworkDataType.d_string);
                    message.Write((string)messageParam);
                }
                else if (messageParam is byte[])
                {
                    message.Write((byte)NetworkDataType.d_byteArray);
                    message.Write(((byte[])messageParam).Length);
                    message.Write((byte[])messageParam);
                }
                else
                {
                    throw new NotImplementedException("Cannot write specified type.");
                }
            }

            //Send the message
            if (recipient == null)
            {
                _mNetManager.ServerSendToAll(message, method);
            }
            else
            {
                _mNetManager.Peer.SendMessage(message, recipient, method);
            }
        }

        private object[] PackParamsForLog(params object[] messageParams)
        {
            var parameters = new List<object>();
            foreach (object messageParam in messageParams)
            {
                if (messageParam.GetType().IsSubclassOf(typeof(Enum)))
                {
                    parameters.Add((int)messageParam);
                }
                else if (messageParam is int
                         || messageParam is uint
                         || messageParam is short
                         || messageParam is ushort
                         || messageParam is long
                         || messageParam is ulong
                         || messageParam is bool
                         || messageParam is float
                         || messageParam is double
                         || messageParam is byte
                         || messageParam is sbyte
                         || messageParam is string)
                {
                    parameters.Add(messageParam);
                }
            }

            return parameters.ToArray();
        }

        /// <summary>
        /// Sends a message to the relevant system(s) on all clients.
        /// </summary>
        public void SendSystemNetworkMessage(EntitySystemMessage message,
                                             NetDeliveryMethod method = NetDeliveryMethod.ReliableUnordered)
        {
            SendSystemNetworkMessage(message, null, method);
        }

        /// <summary>
        /// Sends a message to the relevant system(s) on the target client.
        /// </summary>
        public void SendSystemNetworkMessage(EntitySystemMessage message,
                                     NetConnection targetConnection = null,
                                     NetDeliveryMethod method = NetDeliveryMethod.ReliableUnordered)
        {
            NetOutgoingMessage newMsg = CreateEntityMessage();
            newMsg.Write((byte)EntityMessage.SystemMessage);

            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, message);
                newMsg.Write((int)stream.Length);
                newMsg.Write(stream.ToArray());
            }

            //Send the message
            if (targetConnection != null)
            {
                _mNetManager.Peer.SendMessage(newMsg, targetConnection, method);
            }
            else
            {
                _mNetManager.ServerSendToAll(newMsg, method);
            }
        }

        #endregion Sending

        #region Receiving

        /// <summary>
        /// Handles an incoming entity message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public IncomingEntityMessage HandleEntityNetworkMessage(NetIncomingMessage message)
        {
            var messageType = (EntityMessage)message.ReadByte();
            int uid = 0;
            IncomingEntityMessage incomingEntityMessage = IncomingEntityMessage.Null;
            switch (messageType)
            {
                case EntityMessage.ComponentMessage:
                    uid = message.ReadInt32();
                    incomingEntityMessage = new IncomingEntityMessage(uid, EntityMessage.ComponentMessage,
                                                                      HandleEntityComponentNetworkMessage(message),
                                                                      message.SenderConnection);
                    break;
                case EntityMessage.SystemMessage: //TODO: Not happy with this resolving the entmgr everytime a message comes in.
                    var manager = IoCManager.Resolve<IEntitySystemManager>();
                    manager.HandleSystemMessage(new EntitySystemData(message.SenderConnection, message));
                    break;
                case EntityMessage.PositionMessage:
                    uid = message.ReadInt32();
                    //TODO: Handle position messages!
                    break;
                case EntityMessage.ComponentInstantiationMessage:
                    uid = message.ReadInt32();
                    incomingEntityMessage = new IncomingEntityMessage(uid,
                                                                      EntityMessage.ComponentInstantiationMessage,
                                                                      (uint)
                                                                      UnPackParams(message).First(),
                                                                      message.SenderConnection);
                    break;
            }

            return incomingEntityMessage;
        }

        /// <summary>
        /// Handles an incoming entity component message
        /// </summary>
        /// <param name="message"></param>
        public IncomingEntityComponentMessage HandleEntityComponentNetworkMessage(NetIncomingMessage message)
        {
            var netID = message.ReadUInt32();

            return new IncomingEntityComponentMessage(netID, UnPackParams(message));
        }

        #endregion Receiving

        private List<object> UnPackParams(NetIncomingMessage message)
        {
            var messageParams = new List<object>();
            while (message.Position < message.LengthBits)
            {
                switch ((NetworkDataType)message.ReadByte())
                {
                    case NetworkDataType.d_enum:
                        messageParams.Add(message.ReadInt32()); //Cast from int, because enums are ints.
                        break;
                    case NetworkDataType.d_bool:
                        messageParams.Add(message.ReadBoolean());
                        break;
                    case NetworkDataType.d_byte:
                        messageParams.Add(message.ReadByte());
                        break;
                    case NetworkDataType.d_sbyte:
                        messageParams.Add(message.ReadSByte());
                        break;
                    case NetworkDataType.d_ushort:
                        messageParams.Add(message.ReadUInt16());
                        break;
                    case NetworkDataType.d_short:
                        messageParams.Add(message.ReadInt16());
                        break;
                    case NetworkDataType.d_int:
                        messageParams.Add(message.ReadInt32());
                        break;
                    case NetworkDataType.d_uint:
                        messageParams.Add(message.ReadUInt32());
                        break;
                    case NetworkDataType.d_ulong:
                        messageParams.Add(message.ReadUInt64());
                        break;
                    case NetworkDataType.d_long:
                        messageParams.Add(message.ReadInt64());
                        break;
                    case NetworkDataType.d_float:
                        messageParams.Add(message.ReadFloat());
                        break;
                    case NetworkDataType.d_double:
                        messageParams.Add(message.ReadDouble());
                        break;
                    case NetworkDataType.d_string:
                        messageParams.Add(message.ReadString());
                        break;
                    case NetworkDataType.d_byteArray:
                        int length = message.ReadInt32();
                        messageParams.Add(message.ReadBytes(length));
                        break;
                }
            }
            return messageParams;
        }
    }
}
